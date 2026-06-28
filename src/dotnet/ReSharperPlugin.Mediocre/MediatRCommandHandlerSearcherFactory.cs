using System.Linq;
using JetBrains.Application.Parts;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.ExtensionsAPI;
using JetBrains.ReSharper.Psi.Search;
using JetBrains.ReSharper.Psi.Util;

namespace ReSharperPlugin.Mediocre;

[PsiComponent(Instantiation.DemandAnyThreadSafe)]
public class MediatRCommandHandlerSearcherFactory : DomainSpecificSearcherFactoryBase
{
    public override bool IsCompatibleWithLanguage(PsiLanguageType languageType)
    {
        return languageType is CSharpLanguage;
    }

    public override NavigateTargets GetNavigateToTargets(IDeclaredElement element)
    {
        if (element is IMethod method
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
            var psiModules = element.GetSolution().GetPsiServices().Modules.GetModules();

            foreach (var psiModule in psiModules)
            {
                var scope = psiModule.GetPsiServices().Symbols.GetSymbolScope(
                    psiModule,
                    true,
                    true
                );

                var requestHandlerType = scope
                    .GetAllShortNames()
                    .Where(s => s.StartsWith("IRequestHandler"))
                    .SelectMany(scope.GetElementsByShortName)
                    .FirstOrDefault(e => e is IInterface) as ITypeElement;

                if (requestHandlerType is null)
                {
                    continue;
                }

                var declaredType = TypeFactory.CreateType(requestHandlerType);
                
                var targets = element.GetPsiServices().SingleThreadedFinder.FindAllInheritors(declaredType);

                var declaredElements = targets.Select(target => target.GetTypeElement())
                    .Where(e => e is not null)
                    .SelectMany(e => e.Methods.Where(x => x.ShortName.StartsWith("Handle")))
                    .ToArray();

                return new NavigateTargets(declaredElements, false);
            }
        }

        return NavigateTargets.Empty;
    }
}