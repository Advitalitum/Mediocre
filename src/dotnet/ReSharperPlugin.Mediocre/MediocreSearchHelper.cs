using System.Linq;
using JetBrains.ReSharper.Feature.Services.Navigation.ContextNavigation;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Search;
using JetBrains.ReSharper.Psi.Util;

namespace ReSharperPlugin.Mediocre;

public static class MediocreSearchHelper
{
    public static IDeclaredElement GetRequestHandlerImplementationOrNull(
        DeclaredElementTypeUsageInfo initialTarget,
        IDeclaredElement declaredElement)
    {
        if (IsNotMediatRSendMethod(declaredElement))
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

        var requestHandlerTypeElement = scope
            .GetAllShortNames()
            .Where(s => s.StartsWith("IRequestHandler"))
            .SelectMany(scope.GetElementsByShortName)
            .FirstOrDefault(e => e is IInterface) as ITypeElement;

        if (requestHandlerTypeElement is null)
        {
            return null;
        }

        var requestHandlerInterfaceType = TypeFactory.CreateType(requestHandlerTypeElement);

        var requestHandlerImplementations = declaredElement.GetPsiServices().SingleThreadedFinder
            .FindAllInheritors(requestHandlerInterfaceType);

        var resultDeclaredElement = requestHandlerImplementations.Select(target => target.GetTypeElement())
            .Where(e => e is not null)
            .SelectMany(e =>
                e.Methods
                    .Where(x =>
                        x.ShortName == "Handle"
                        && x.ReturnType.IsGenericTask(out var responseType)
                        && requestHandlerResponseType.Equals(responseType.GetTypeElement())
                    )
            )
            .FirstOrDefault();

        return resultDeclaredElement;
    }

    private static bool IsNotMediatRSendMethod(IDeclaredElement declaredElement)
    {
        return declaredElement is not IMethod method
               || method.ShortName != "Send"
               || method.TypeParametersCount != 1
               || method.ContainingType is not IInterface @interface
               || @interface.GetContainingNamespace().ShortName != "MediatR"
               || method.Parameters.Count < 1
               || method.Parameters[0].Type.GetTypeElement() is not IInterface parameterInterface
               || parameterInterface.ShortName != "IRequest"
               || parameterInterface.GetContainingNamespace().ShortName != "MediatR"
               || parameterInterface.TypeParametersCount != 1;
    }
}