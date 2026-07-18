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
        var searchHandlerRequest = MediocreSearchHelper.CreateSearchHandlerRequestOrNull(context, element, initialTarget);
        
        return searchHandlerRequest ?? base.CreateSearchRequest(context, element, initialTarget);
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