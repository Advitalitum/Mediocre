using System.Linq;
using JetBrains.Application.DataContext;
using JetBrains.ReSharper.Feature.Services.Navigation.ContextNavigation;
using JetBrains.ReSharper.Feature.Services.Navigation.Requests;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Search;
using JetBrains.ReSharper.Psi.Util;

namespace ReSharperPlugin.Mediocre;

public static class MediocreSearchHelper
{
    public static SearchImplementationsRequest CreateSearchHandlerRequestOrNull(IDataContext context,
        DeclaredElementTypeUsageInfo element, DeclaredElementTypeUsageInfo initialTarget)
    {
        var declaredElement = element.DeclaredElement;

        var resultDeclaredElement = GetRequestHandlerImplementationOrNull(initialTarget, declaredElement);

        if (resultDeclaredElement is not null)
        {
            var searchDomain = SearchDomainContextUtil.GetSearchDomainContext(context).GetDefaultDomain().SearchDomain;

            return new SearchImplementationsRequest(resultDeclaredElement, searchDomain);
        }

        return null;
    }

    private static IDeclaredElement GetRequestHandlerImplementationOrNull(
        DeclaredElementTypeUsageInfo initialTarget,
        IDeclaredElement declaredElement)
    {
        if (IsMediatRSendMethod(declaredElement) is false)
        {
            return null;
        }

        var method = (IMethod) declaredElement;

        var requestHandlerResponseType = initialTarget
            .Substitution[method.TypeParameters.First()].GetScalarType()?.GetTypeElement();

        if (requestHandlerResponseType is null or IInterface)
        {
            return null;
        }

        var psiModule = method.Module;

        var scope = psiModule
            .GetPsiServices()
            .Symbols
            .GetSymbolScope(psiModule, true, true);

        var requestHandlerTypeElements = scope
            .GetAllShortNames()
            .Where(s => s.StartsWith("IRequestHandler"))
            .SelectMany(scope.GetElementsByShortName)
            .Where(e => e is IInterface)
            .OfType<ITypeElement>()
            .ToArray();

        if (requestHandlerTypeElements.Any() is false)
        {
            return null;
        }

        var requestHandlerInterfaceTypes = requestHandlerTypeElements.Select(TypeFactory.CreateType);

        var requestHandlerImplementations = requestHandlerInterfaceTypes
            .SelectMany(x => declaredElement.GetPsiServices().SingleThreadedFinder.FindAllInheritors(x));

        var resultDeclaredElement = requestHandlerImplementations.Select(target => target.GetTypeElement())
            .Where(e => e is not null)
            .SelectMany(e =>
                e.Methods
                    .Where(x =>
                        x.ShortName == "Handle"
                        && (
                            (x.ReturnType.IsGenericTask(out var responseType) && requestHandlerResponseType.Equals(responseType.GetTypeElement()))
                            || (x.ReturnType.IsTask() && x.Parameters.Count >= 1 && requestHandlerResponseType.Equals(x.Parameters[0].Type.GetTypeElement()))
                        )
                    )
            )
            .FirstOrDefault();

        return resultDeclaredElement;
    }

    private static bool IsMediatRSendMethod(IDeclaredElement declaredElement)
    {
        return declaredElement is IMethod { ShortName: "Send" } method
               && method.Module.Name.StartsWith("MediatR")
               && method.TypeParametersCount == 1
               && method.ContainingType is IInterface @interface
               && @interface.ShortName == "ISender"
               && @interface.GetContainingNamespace().ShortName == "MediatR"
               && method.Parameters.Count >= 1
               && ((method.Parameters[0].Type.GetTypeElement() is IInterface parameterInterface
                    && parameterInterface.ShortName == "IRequest"
                    && parameterInterface.GetContainingNamespace().ShortName == "MediatR"
                    && parameterInterface.TypeParametersCount == 1)
                   || method.TypeParameters.Single().TypeConstraints.FirstOrDefault()
                       ?.GetPresentableName(method.PresentationLanguage) == "IRequest");
    }
}