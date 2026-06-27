using System.Linq;
using JetBrains.Application.Parts;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.ExtensionsAPI;
using JetBrains.ReSharper.Psi.Search;
using JetBrains.ReSharper.Psi.Util;

namespace ReSharperPlugin.Mediocre;

[PsiComponent(Instantiation.DemandAnyThreadSafe)]
public class MediatrSearcherFactory : DomainSpecificSearcherFactoryBase
{
    public override bool IsCompatibleWithLanguage(PsiLanguageType languageType)
    {
        return languageType is CSharpLanguage;
    }

    public override NavigateTargets GetNavigateToTargets(IDeclaredElement element)
    {
        if (element.PresentationLanguage is CSharpLanguage
            && element is IMethod method
            && method.ShortName == "Send"
            && method.TypeParametersCount == 1
            && method.ContainingType is IInterface @interface
            && @interface.GetContainingNamespace().ShortName == "MediatR"
            && method.Parameters.Count >= 1
            && method.Parameters[0].Type is IDeclaredType declaredType
            && method.Parameters[0].Type.GetTypeElement() is IInterface parameterInterface
            && parameterInterface.ShortName == "IRequest"
            && parameterInterface.GetContainingNamespace().ShortName == "MediatR"
            && parameterInterface.TypeParametersCount == 1
           )
        {
            var targets = element.GetPsiServices().SingleThreadedFinder.FindAllInheritors(declaredType);
            var declaredElements = targets.Select(target => target.GetTypeElement()).ToArray();
            
            return new NavigateTargets(declaredElements, false);
        }

        return NavigateTargets.Empty;
    }
}