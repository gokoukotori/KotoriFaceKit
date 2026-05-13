namespace Aoyon.FaceTune
{
    [AddComponentMenu(MenuPath)]
    public class BaseExpressionDataComponent : ExpressionDataSourceComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Base Expression Data";
        internal const string MenuPath = BasePath + "/" + Expression + "/" + ComponentName;
    }
}
