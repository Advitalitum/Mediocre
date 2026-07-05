using System.Linq;
using JetBrains.Application;
using JetBrains.Application.DataContext;
using JetBrains.ReSharper.Feature.Services.Navigation.ContextNavigation;
using JetBrains.ReSharper.Feature.Services.Navigation.Requests;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Search;
using JetBrains.ReSharper.Psi.Util;

namespace ReSharperPlugin.Mediocre;

[ShellFeaturePart]
public class RequestHandlerImplementationContextSearch : ImplementationContextSearch
{
    protected override SearchImplementationsRequest CreateSearchRequest(IDataContext context,
        DeclaredElementTypeUsageInfo element,
        DeclaredElementTypeUsageInfo initialTarget)
    {
        var declaredElement = element.DeclaredElement;

        if (declaredElement is IMethod method
            && method.ShortName == "Send"
            && method.TypeParametersCount == 1
            && method.ContainingType is IInterface @interface
            && @interface.GetContainingNamespace().ShortName == "MediatR"
            && method.Parameters.Count >= 1
            && method.Parameters[0].Type.GetTypeElement() is IInterface parameterInterface
            && parameterInterface.ShortName == "IRequest"
            && parameterInterface.GetContainingNamespace().ShortName == "MediatR"
            && parameterInterface.TypeParametersCount == 1
           )
        {
            var psiModule = @interface.Module;

            var scope = psiModule.GetPsiServices().Symbols.GetSymbolScope(
                psiModule,
                true,
                true
            );

            var requestHandlerTypeElement = scope
                .GetAllShortNames()
                .Where(s => s.StartsWith("IRequestHandler"))
                .SelectMany(scope.GetElementsByShortName)
                .FirstOrDefault(e => e is IInterface) as ITypeElement;

            if (requestHandlerTypeElement is null)
            {
                return base.CreateSearchRequest(context, element, initialTarget);
            }

            var requestHandlerInterfaceType = TypeFactory.CreateType(requestHandlerTypeElement);

            var requestHandlerImplementations = declaredElement.GetPsiServices().SingleThreadedFinder
                .FindAllInheritors(requestHandlerInterfaceType);

            var commandResponseType = initialTarget
                .Substitution[(declaredElement as IMethod).TypeParameters.First()].GetScalarType()?.GetTypeElement();

            if (commandResponseType is null)
            {
                return base.CreateSearchRequest(context, element, initialTarget);
            }

            var resultDeclaredElement = requestHandlerImplementations.Select(target => target.GetTypeElement())
                .Where(e => e is not null)
                .SelectMany(e =>
                    e.Methods
                    .Where(x =>
                        x.ShortName == "Handle"
                        && x.ReturnType.IsGenericTask(out var responseType)
                        && commandResponseType.Equals(responseType.GetTypeElement())
                    )
                )
                .FirstOrDefault();

            if (resultDeclaredElement is null)
            {
                return base.CreateSearchRequest(context, element, initialTarget);
            }

            var searchDomain = SearchDomainContextUtil.GetSearchDomainContext(context).GetDefaultDomain().SearchDomain;

            return new SearchImplementationsRequest(resultDeclaredElement, searchDomain);
        }

        return base.CreateSearchRequest(context, element, initialTarget);
    }
}

// [PsiComponent(Instantiation.DemandAnyThreadSafe)]
// public class MediatRCommandHandlerSearcherFactory : DomainSpecificSearcherFactoryBase
// {
//     public override bool IsCompatibleWithLanguage(PsiLanguageType languageType)
//     {
//         return languageType is CSharpLanguage;
//     }
//
//     public override DerivedFindRequest GetDerivedFindRequest(IFindResultReference result)
//     {
//         var ex = result;
//         var reference = result.Reference;
//         var names = reference.GetAllNames();
//
//         return new DerivedFindRequest([result.DeclaredElement], x => true, true);
//         return base.GetDerivedFindRequest(result);
//     }
//
//     // public override NavigateTargets GetNavigateToTargets(IDeclaredElement element)
//     // {
//     //     if (element is IMethod method
//     //         && method.ShortName == "Send"
//     //         && method.TypeParametersCount == 1
//     //         && method.ContainingType is IInterface @interface
//     //         && @interface.GetContainingNamespace().ShortName == "MediatR"
//     //         && method.Parameters.Count >= 1
//     //         && method.Parameters[0].Type.GetTypeElement() is IInterface parameterInterface
//     //         && parameterInterface.ShortName == "IRequest"
//     //         && parameterInterface.GetContainingNamespace().ShortName == "MediatR"
//     //         && parameterInterface.TypeParametersCount == 1
//     //        )
//     //     {
//     //         var psiModules = element.GetSolution().GetPsiServices().Modules.GetModules();
//     //         
//     //         foreach (var psiModule in psiModules)
//     //         {
//     //             var scope = psiModule.GetPsiServices().Symbols.GetSymbolScope(
//     //                 psiModule,
//     //                 true,
//     //                 true
//     //             );
//     //
//     //             var requestHandlerType = scope
//     //                 .GetAllShortNames()
//     //                 .Where(s => s.StartsWith("IRequestHandler"))
//     //                 .SelectMany(scope.GetElementsByShortName)
//     //                 .FirstOrDefault(e => e is IInterface) as ITypeElement;
//     //
//     //             if (requestHandlerType is null)
//     //             {
//     //                 continue;
//     //             }
//     //
//     //             var declaredType = TypeFactory.CreateType(requestHandlerType);
//     //             
//     //             var targets = element.GetPsiServices().SingleThreadedFinder.FindAllInheritors(declaredType);
//     //
//     //             var declaredElements = targets.Select(target => target.GetTypeElement())
//     //                 .Where(e => e is not null)
//     //                 .SelectMany(e => e.Methods.Where(x => x.ShortName.StartsWith("Handle")))
//     //                 .ToArray();
//     //
//     //             return new NavigateTargets(declaredElements, false);
//     //         }
//     //     }
//     //
//     //     return NavigateTargets.Empty;
//     // }
// }