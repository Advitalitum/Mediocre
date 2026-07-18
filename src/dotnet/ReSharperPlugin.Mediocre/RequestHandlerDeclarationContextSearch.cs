using JetBrains.Application;
using JetBrains.Application.DataContext;
using JetBrains.ReSharper.Feature.Services.Navigation.ContextNavigation;
using JetBrains.ReSharper.Feature.Services.Navigation.Requests;

namespace ReSharperPlugin.Mediocre;

[ShellFeaturePart]
public class RequestHandlerDeclarationContextSearch : DefaultDeclarationSearchBase
{
    protected override bool IsExecuteImmediately { get; } = true;

    protected override SearchDeclarationsRequest CreateSearchRequest(IDataContext context,
        DeclaredElementTypeUsageInfo element,
        DeclaredElementTypeUsageInfo initialTarget)
    {
        var searchHandlerRequest = MediocreSearchHelper.CreateSearchHandlerRequestOrNull(context, element, initialTarget);
        
        return searchHandlerRequest ?? base.CreateSearchRequest(context, element, initialTarget);
    }
}