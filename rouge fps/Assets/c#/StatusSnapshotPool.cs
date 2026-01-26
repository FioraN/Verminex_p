using System.Collections.Generic;

public static class StatusSnapshotPool
{
    private static readonly Stack<List<StatusSnapshot>> Pool = new Stack<List<StatusSnapshot>>();

    public static List<StatusSnapshot> Get()
        => Pool.Count > 0 ? Pool.Pop() : new List<StatusSnapshot>(8);

    public static void Release(List<StatusSnapshot> list)
    {
        if (list == null) return;
        list.Clear();
        Pool.Push(list);
    }
}
