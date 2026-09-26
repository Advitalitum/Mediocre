using System;
using System.Collections.Concurrent;
using System.Linq;
using JetBrains.Application.DataContext;
using JetBrains.ReSharper.Feature.Services.Navigation.ContextNavigation;
using JetBrains.ReSharper.Feature.Services.Navigation.Requests;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Modules;
using JetBrains.ReSharper.Psi.Search;
using JetBrains.ReSharper.Psi.Util;

namespace ReSharperPlugin.Mediocre;

public static class MediocreSearchHelper
{
    private static readonly
        ConcurrentDictionary<(IPsiModule Module, MediatrISenderMethod MediatrISenderMethod), ITypeElement>
        RequestHandlersCache = new();

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
        var mediatrISenderMethod = GetMediatrISenderMethod(declaredElement);
        
        if (mediatrISenderMethod is MediatrISenderMethod.None)
        {
            return null;
        }

        var method = (IMethod) declaredElement;

        var typeParameterType = initialTarget
            .Substitution[method.TypeParameters.First()].GetScalarType()?.GetTypeElement();

        if (typeParameterType is null)
        {
            return null;
        }

        var requestHandlerTypeElement = GetRequestHandlerImplementationOrNull(method, mediatrISenderMethod);

        if (requestHandlerTypeElement is null)
        {
            return null;
        }

        if (typeParameterType is IInterface { ShortName: "IRequest" } @interface && @interface.GetContainingNamespace().ShortName == "MediatR")
        {
            // Cannot determine concrete command type, just return matching handler interface
            
            // return requestHandlerTypeElements.Single().Methods.FirstOrDefault(x => x.ShortName == "Handle" &&
            //     method.ReturnType.IsTask()
            //     && method.Parameters.Count >= 1
            //     && method.Parameters[0].Type.GetTypeElement() is not null
            //     && method.Parameters[0].Type.GetTypeElement().GetSuperTypes()
            //         .Any(y => y.GetTypeElement() is not null && y.GetTypeElement().Equals(typeParameterType)));

            return requestHandlerTypeElement;
        }

        var requestHandlerInterfaceType = TypeFactory.CreateType(requestHandlerTypeElement);

        var requestHandlerImplementations = declaredElement
            .GetPsiServices()
            .SingleThreadedFinder
            .FindAllInheritors(requestHandlerInterfaceType);

        var resultDeclaredElement = requestHandlerImplementations
            .Select(target => target.GetTypeElement())
            .Where(e => e is not null)
            .Select(e => e.Methods.FirstOrDefault(x => GetMethodPredicate(x, typeParameterType, mediatrISenderMethod)))
            .FirstOrDefault(m => m is not null);

        return resultDeclaredElement;
    }

    private static ITypeElement GetRequestHandlerImplementationOrNull(IMethod method,
        MediatrISenderMethod mediatrISenderMethod)
    {
        var fromCache = RequestHandlersCache.GetOrAdd((method.Module, mediatrISenderMethod), AddToCache);

        return fromCache;
    }

    private static ITypeElement AddToCache((IPsiModule Module, MediatrISenderMethod MediatrISenderMethod) key)
    {
        var psiModule = key.Module;
        var mediatrISenderMethod = key.MediatrISenderMethod;

        var scope = psiModule
            .GetPsiServices()
            .Symbols
            .GetSymbolScope(psiModule, true, true);

        var requestHandlerTypeElements = scope
            .GetAllShortNames()
            .Where(s => s.StartsWith(GetHandlerInterfaceName(mediatrISenderMethod)))
            .SelectMany(scope.GetElementsByShortName)
            .Where(e => e is IInterface)
            .OfType<ITypeElement>()
            .Where(x => HasAppropriateTypeParametersCount(x, mediatrISenderMethod))
            .ToArray();

        return requestHandlerTypeElements.Length == 1 ? requestHandlerTypeElements.Single() : null;
    }

    private static bool HasAppropriateTypeParametersCount(ITypeElement requestHandlerTypeElement,
        MediatrISenderMethod mediatrISenderMethod)
    {
        var typeParametersCount = mediatrISenderMethod switch
        {
            MediatrISenderMethod.SendWithResponse or MediatrISenderMethod.CreateStream => 2,
            MediatrISenderMethod.SendWithoutResponse => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(mediatrISenderMethod), mediatrISenderMethod, null)
        };

        return requestHandlerTypeElement.TypeParametersCount == typeParametersCount;
    }

    private static bool GetMethodPredicate(IMethod method, ITypeElement typeParameterType,
        MediatrISenderMethod mediatrISenderMethod)
    {
        if (method.ShortName != "Handle")
        {
            return false;
        }

        var result = mediatrISenderMethod switch
        {
            MediatrISenderMethod.SendWithResponse =>
                method.ReturnType.IsGenericTask(out var responseType)
                && typeParameterType.Equals(responseType.GetTypeElement()),

            MediatrISenderMethod.SendWithoutResponse =>
                method.ReturnType.IsTask()
                && method.Parameters.Count >= 1
                && typeParameterType.Equals(method.Parameters[0].Type.GetTypeElement()),

            MediatrISenderMethod.CreateStream =>
                method.ReturnType.IsIAsyncEnumerable()
                && typeParameterType.Equals(method.ReturnType
                    .GetGenericUnderlyingType(method.ReturnType.GetTypeElement()).GetTypeElement()),

            _ => throw new ArgumentOutOfRangeException(nameof(mediatrISenderMethod), mediatrISenderMethod, null)
        };

        return result;
    }

    private static string GetHandlerInterfaceName(MediatrISenderMethod mediatrISenderMethod)
    {
        var result = mediatrISenderMethod switch
        {
            MediatrISenderMethod.SendWithResponse or MediatrISenderMethod.SendWithoutResponse => "IRequestHandler",
            MediatrISenderMethod.CreateStream => "IStreamRequestHandler",
            _ => throw new ArgumentOutOfRangeException(nameof(mediatrISenderMethod), mediatrISenderMethod, null)
        };

        return result;
    }

    private static MediatrISenderMethod GetMediatrISenderMethod(IDeclaredElement declaredElement)
    {
        var method = declaredElement as IMethod;
        var isMediatrISenderGenericMethod = method is { ShortName: "Send" or "CreateStream" }
                                            && method.Module.Name.StartsWith("MediatR")
                                            && method.TypeParametersCount == 1
                                            && method.ContainingType is IInterface @interface
                                            && @interface.ShortName == "ISender"
                                            && @interface.GetContainingNamespace().ShortName == "MediatR"
                                            && method.Parameters.Count >= 1;

        if (isMediatrISenderGenericMethod is false)
        {
            return MediatrISenderMethod.None;
        }

        var isSendWithResponse = method.ShortName == "Send"
                                 && method.Parameters[0].Type.GetTypeElement() is IInterface sendParameterInterface
                                 && sendParameterInterface.ShortName == "IRequest"
                                 && sendParameterInterface.GetContainingNamespace().ShortName == "MediatR"
                                 && sendParameterInterface.TypeParametersCount == 1;

        if (isSendWithResponse)
        {
            return MediatrISenderMethod.SendWithResponse;
        }

        var isSendWithoutResponse = method.ShortName == "Send"
                                    && method.TypeParameters
                                        .Single()
                                        .TypeConstraints
                                        .FirstOrDefault()?
                                        .GetPresentableName(method.PresentationLanguage) == "IRequest";

        if (isSendWithoutResponse)
        {
            return MediatrISenderMethod.SendWithoutResponse;
        }

        var isCreateStreamMethod = method.ShortName == "CreateStream"
                                   && method.Parameters[0].Type.GetTypeElement() is IInterface
                                       createStreamParameterInterface
                                   && createStreamParameterInterface.ShortName == "IStreamRequest"
                                   && createStreamParameterInterface.GetContainingNamespace().ShortName == "MediatR"
                                   && createStreamParameterInterface.TypeParametersCount == 1;

        if (isCreateStreamMethod)
        {
            return MediatrISenderMethod.CreateStream;
        }

        return MediatrISenderMethod.None;
    }

    private enum MediatrISenderMethod
    {
        None,
        SendWithResponse,
        SendWithoutResponse,
        CreateStream
    }
}