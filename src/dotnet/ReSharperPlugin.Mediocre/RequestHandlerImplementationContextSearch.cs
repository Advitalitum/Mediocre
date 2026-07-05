using System.Collections.Generic;
using System.Linq;
using JetBrains.Application;
using JetBrains.Application.DataContext;
using JetBrains.ReSharper.Feature.Services.Navigation.ContextNavigation;
using JetBrains.ReSharper.Feature.Services.Navigation.Requests;
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

        var resultDeclaredElement =
            MediocreSearchHelper.GetRequestHandlerImplementationOrNull(initialTarget, declaredElement);

        if (resultDeclaredElement is not null)
        {
            var searchDomain = SearchDomainContextUtil.GetSearchDomainContext(context).GetDefaultDomain().SearchDomain;

            return new SearchImplementationsRequest(resultDeclaredElement, searchDomain);
        }

        return base.CreateSearchRequest(context, element, initialTarget);
    }

    protected override IEnumerable<DeclaredElementTypeUsageInfo> GetElementCandidates(IDataContext context,
        ReferencePreferenceKind kind, bool updateOnly)
    {
        var candidates = base.GetElementCandidates(context, kind, updateOnly);

        var lastOrDefault = candidates.LastOrDefault();

        return lastOrDefault is null ? [] : [lastOrDefault];
    }

    protected override bool IsExecuteImmediately { get; } = true;
}

[ShellFeaturePart]
public class RequestHandlerDeclarationContextSearch : DefaultDeclarationSearchBase
{
    protected override bool IsExecuteImmediately { get; } = true;

    protected override SearchDeclarationsRequest CreateSearchRequest(IDataContext context,
        DeclaredElementTypeUsageInfo element,
        DeclaredElementTypeUsageInfo initialTarget)
    {
        var declaredElement = element.DeclaredElement;

        var resultDeclaredElement =
            MediocreSearchHelper.GetRequestHandlerImplementationOrNull(initialTarget, declaredElement);

        if (resultDeclaredElement is not null)
        {
            var searchDomain = SearchDomainContextUtil.GetSearchDomainContext(context).GetDefaultDomain().SearchDomain;

            return new SearchImplementationsRequest(resultDeclaredElement, searchDomain);
        }

        return base.CreateSearchRequest(context, element, initialTarget);
    }
}