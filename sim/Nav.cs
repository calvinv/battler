using System.Collections.Generic;

namespace Battler.Sim;

/// <summary>
/// Finding a way round what can't be crossed (Ground.Passable: the lakes, the ridge's cliffs, ponds). The map is cut
/// into Cell-unit squares, open or shut (shut too if the edge of a unit standing there would be in the way, so paths
/// keep clear of walls); A* finds a chain of squares, then the chain is pulled straight wherever there's a clear
/// line, so units drive in straight runs from corner to corner. Integers and fixed point only: every machine finds
/// the same path. One per World (its search buffers), the squares shared.
/// </summary>
public sealed class Nav
{
    public const int Cell = 2;
    static readonly int W = World.MapHalfX.ToInt() * 2 / Cell, H = World.MapHalfZ.ToInt() * 2 / Cell;
    static readonly int X0 = -World.MapHalfX.ToInt(), Z0 = -World.MapHalfZ.ToInt();
    static readonly bool[] Open = BuildOpen();

    static bool[] BuildOpen()
    {
        var open = new bool[W * H];
        for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++)
            {
                var c = Centre(j * W + i);
                bool ok = Ground.Passable(c);
                foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })   // a unit's width of room
                    ok &= Ground.Passable(c + new FixVec2(dx, dz));
                open[j * W + i] = ok;
            }
        return open;
    }

    static FixVec2 Centre(int cell) => new(X0 + (cell % W) * Cell + Cell / 2, Z0 + (cell / W) * Cell + Cell / 2);

    static int CellOf(FixVec2 p)
    {
        int i = (p.X.ToInt() - X0) / Cell, j = (p.Z.ToInt() - Z0) / Cell;
        if (p.X.ToInt() < X0) i = -1;
        if (p.Z.ToInt() < Z0) j = -1;
        return i < 0 || j < 0 || i >= W || j >= H ? -1 : j * W + i;
    }

    /// <summary>Whether a ground unit can stand here.</summary>
    public static bool OpenAt(FixVec2 p) { int c = CellOf(p); return c >= 0 && Open[c]; }

    /// <summary>Whether the straight line from a to b stays on open ground (checked every unit along it).</summary>
    public static bool Clear(FixVec2 a, FixVec2 b)
    {
        var d = b - a;
        int steps = d.Length.ToInt() + 1;
        for (int k = 0; k <= steps; k++)
            if (!OpenAt(a + d * Fix.Ratio(k, steps))) return false;
        return true;
    }

    // ---- the search (per World) ----------------------------------------------------------------------------------

    readonly int[] _g = new int[W * H], _from = new int[W * H], _seen = new int[W * H], _done = new int[W * H];
    int _stamp;
    readonly List<long> _heap = new();

    /// <summary>The nearest open square to a square (itself if open), searching outward; -1 if none near.</summary>
    static int NearestOpen(int cell, FixVec2 p)
    {
        if (cell >= 0 && Open[cell]) return cell;
        int ci = (p.X.ToInt() - X0) / Cell, cj = (p.Z.ToInt() - Z0) / Cell;
        for (int r = 1; r < 25; r++)
            for (int dj = -r; dj <= r; dj++)
                for (int di = -r; di <= r; di++)
                {
                    if (System.Math.Abs(di) != r && System.Math.Abs(dj) != r) continue;   // the ring only
                    int i = ci + di, j = cj + dj;
                    if (i >= 0 && j >= 0 && i < W && j < H && Open[j * W + i]) return j * W + i;
                }
        return -1;
    }

    static int Estimate(int a, int b)
    {
        int dx = System.Math.Abs(a % W - b % W), dz = System.Math.Abs(a / W - b / W);
        return 10 * (dx + dz) - 6 * System.Math.Min(dx, dz);   // octile: 10 a side, 14 a diagonal
    }

    /// <summary>A way from `from` to `to`: corner points to drive through, ending at `to` (or the nearest open spot to
    /// it). Null if there's none.</summary>
    public List<FixVec2> Path(FixVec2 from, FixVec2 to)
    {
        int start = NearestOpen(CellOf(from), from), goal = NearestOpen(CellOf(to), to);
        if (start < 0 || goal < 0) return null;
        int toCell = CellOf(to);
        var end = toCell >= 0 && Open[toCell] ? to : Centre(goal);   // (into the water or up the cliff: as near as it gets)
        if (start == goal) return new List<FixVec2> { end };
        _stamp++;
        _heap.Clear();
        _g[start] = 0; _seen[start] = _stamp; _from[start] = -1;
        Push(Estimate(start, goal), start);
        bool found = false;
        int expanded = 0;
        while (_heap.Count > 0 && expanded++ < W * H)
        {
            int cur = Pop();
            if (_done[cur] == _stamp) continue;
            _done[cur] = _stamp;
            if (cur == goal) { found = true; break; }
            int ci = cur % W, cj = cur / W;
            for (int dj = -1; dj <= 1; dj++)
                for (int di = -1; di <= 1; di++)
                {
                    if (di == 0 && dj == 0) continue;
                    int i = ci + di, j = cj + dj;
                    if (i < 0 || j < 0 || i >= W || j >= H) continue;
                    int next = j * W + i;
                    if (!Open[next] || _done[next] == _stamp) continue;
                    if (di != 0 && dj != 0 && (!Open[cj * W + i] || !Open[j * W + ci])) continue;   // no cutting corners
                    int g = _g[cur] + (di != 0 && dj != 0 ? 14 : 10);
                    if (_seen[next] == _stamp && g >= _g[next]) continue;
                    _seen[next] = _stamp; _g[next] = g; _from[next] = cur;
                    Push(g + Estimate(next, goal), next);
                }
        }
        if (!found) return null;
        // The squares back from the goal, then pulled straight: from where we are, on to the furthest one in clear view.
        var cells = new List<FixVec2>();
        for (int c = goal; c >= 0 && c != start; c = _from[c]) cells.Add(Centre(c));
        cells.Reverse();
        cells[^1] = end;
        var path = new List<FixVec2>();
        var at = from;
        int k = 0;
        while (k < cells.Count)
        {
            int far = k;
            for (int m = cells.Count - 1; m > k; m--)
                if (Clear(at, cells[m])) { far = m; break; }
            path.Add(cells[far]);
            at = cells[far];
            k = far + 1;
        }
        return path;
    }

    // A binary heap of (cost << 20 | square): cheapest first, ties broken by square number, so every machine agrees.
    void Push(int cost, int cell)
    {
        long key = ((long)cost << 20) | (uint)cell;
        _heap.Add(key);
        int i = _heap.Count - 1;
        while (i > 0)
        {
            int up = (i - 1) / 2;
            if (_heap[up] <= _heap[i]) break;
            (_heap[up], _heap[i]) = (_heap[i], _heap[up]);
            i = up;
        }
    }

    int Pop()
    {
        long top = _heap[0];
        _heap[0] = _heap[^1];
        _heap.RemoveAt(_heap.Count - 1);
        int i = 0;
        while (true)
        {
            int l = i * 2 + 1, r = l + 1, least = i;
            if (l < _heap.Count && _heap[l] < _heap[least]) least = l;
            if (r < _heap.Count && _heap[r] < _heap[least]) least = r;
            if (least == i) break;
            (_heap[least], _heap[i]) = (_heap[i], _heap[least]);
            i = least;
        }
        return (int)(top & 0xFFFFF);
    }
}
