namespace EastFive.Api.Meta.Flows.Scripted
{
    /// <summary>
    /// Sentinel return type for scripted flow expressions. A flow is authored as a single
    /// <c>System.Linq.Expressions.Expression&lt;System.Func&lt;TApi, FlowNode&gt;&gt;</c>; the
    /// value is never produced at runtime — only the shape of the captured expression tree
    /// matters. <see cref="FlowScriptReader"/> walks that tree to emit a Postman collection.
    /// </summary>
    public readonly struct FlowNode
    {
    }
}
