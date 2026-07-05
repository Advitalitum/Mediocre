using JetBrains.Application;
using JetBrains.Application.DataContext;
using JetBrains.ReSharper.Feature.Services.Navigation.ContextNavigation;
using JetBrains.ReSharper.Feature.Services.Navigation.Requests;

namespace ReSharperPlugin.Mediocre;

[ShellFeaturePart]
public class RequestHandlerImplementationContextSearch : ImplementationContextSearch
{
    protected override SearchImplementationsRequest CreateSearchRequest(IDataContext context,
        DeclaredElementTypeUsageInfo element,
        DeclaredElementTypeUsageInfo initialTarget)
    {
        var declaredElement = element.DeclaredElement;

        var resultDeclaredElement = MediocreSearchHelper.GetRequestHandlerImplementationOrNull(initialTarget, declaredElement);

        if (resultDeclaredElement is not null)
        {
            var searchDomain = SearchDomainContextUtil.GetSearchDomainContext(context).GetDefaultDomain().SearchDomain;

            return new SearchImplementationsRequest(resultDeclaredElement, searchDomain);
        }

        return base.CreateSearchRequest(context, element, initialTarget);
    }
}

[ShellFeaturePart]
public class RequestHandlerDeclarationContextSearch : DefaultDeclarationSearchBase 
{
    protected override SearchDeclarationsRequest CreateSearchRequest(IDataContext context,
        DeclaredElementTypeUsageInfo element,
        DeclaredElementTypeUsageInfo initialTarget)
    {
        var declaredElement = element.DeclaredElement;

        var resultDeclaredElement = MediocreSearchHelper.GetRequestHandlerImplementationOrNull(initialTarget, declaredElement);

        if (resultDeclaredElement is not null)
        {
            var searchDomain = SearchDomainContextUtil.GetSearchDomainContext(context).GetDefaultDomain().SearchDomain;

            return new SearchImplementationsRequest(resultDeclaredElement, searchDomain);
        }

        return base.CreateSearchRequest(context, element, initialTarget);
    }
}