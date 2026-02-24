using System.Collections.Generic;
using UnityEngine;

// 节点状态
public enum NodeState
{
    Running,
    Success,
    Failure
}

// 基础节点类
public abstract class Node
{
    protected NodeState state;

    public virtual NodeState Evaluate()
    {
        return NodeState.Failure;
    }
}

// 依次执行子节点，直到其中一个失败，或者全部成功。
public class Sequence : Node
{
    protected List<Node> nodes = new List<Node>();

    public Sequence(List<Node> nodes)
    {
        this.nodes = nodes;
    }

    public override NodeState Evaluate()
    {
        bool anyChildRunning = false;

        foreach (var node in nodes)
        {
            switch (node.Evaluate())
            {
                case NodeState.Failure:
                    state = NodeState.Failure;
                    return state;
                case NodeState.Success:
                    continue;
                case NodeState.Running:
                    anyChildRunning = true;
                    continue;
                default:
                    state = NodeState.Success;
                    return state;
            }
        }

        state = anyChildRunning ? NodeState.Running : NodeState.Success;
        return state;
    }
}

// 依次执行子节点，只要有一个成功就返回成功。
public class Selector : Node
{
    protected List<Node> nodes = new List<Node>();

    public Selector(List<Node> nodes)
    {
        this.nodes = nodes;
    }

    public override NodeState Evaluate()
    {
        foreach (var node in nodes)
        {
            switch (node.Evaluate())
            {
                case NodeState.Failure:
                    continue;
                case NodeState.Success:
                    state = NodeState.Success;
                    return state;
                case NodeState.Running:
                    state = NodeState.Running;
                    return state;
                default:
                    continue;
            }
        }

        state = NodeState.Failure;
        return state;
    }
}




