using System;
using System.Collections.Generic;
using UnityEngine;

namespace Naval
{
    /// <summary>
    /// Coarse navigation grid built from the height field. Passability is evaluated per ship draft,
    /// so a destroyer can cut through shoals a battleship has to sail around.
    /// Requests are time sliced - only a few A* solves run per frame.
    /// </summary>
    public class NavGrid : MonoBehaviour
    {
        public static NavGrid I { get; private set; }

        public int W { get; private set; }
        public int H { get; private set; }
        public float CellSize { get; private set; }

        float[] _depth;        // normalised depth per cell (negative over land)
        byte[] _coast;         // cells of distance to nearest land, saturating at 255

        // A* working set
        float[] _g;
        int[] _from;
        int[] _stamp;
        int _currentStamp;
        int[] _heap;
        float[] _heapKey;
        int _heapCount;
        int[] _heapIndex;

        class Request
        {
            public Vector2 start, goal;
            public float draft;
            public Action<List<Vector2>> callback;
            public bool cancelled;
        }

        readonly Queue<Request> _queue = new Queue<Request>();
        public int PendingRequests => _queue.Count;
        public int SolvesThisFrame { get; private set; }

        public static NavGrid Create(Transform parent)
        {
            var go = new GameObject("NavGrid");
            go.transform.SetParent(parent, false);
            var g = go.AddComponent<NavGrid>();
            I = g;
            return g;
        }

        public void Build(WorldMap map, float cellSize = GameConfig.NavCellSize)
        {
            I = this;
            CellSize = cellSize;
            W = Mathf.CeilToInt(map.Size / cellSize);
            H = W;
            _depth = new float[W * H];
            _coast = new byte[W * H];

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Vector2 c = CellCenter(x, y);
                    // sample the cell plus its corners so a thin spit of land still blocks the cell
                    float d = map.SampleDepth(c);
                    float o = cellSize * 0.45f;
                    d = Mathf.Min(d, map.SampleDepth(c + new Vector2(o, o)));
                    d = Mathf.Min(d, map.SampleDepth(c + new Vector2(-o, o)));
                    d = Mathf.Min(d, map.SampleDepth(c + new Vector2(o, -o)));
                    d = Mathf.Min(d, map.SampleDepth(c + new Vector2(-o, -o)));
                    _depth[y * W + x] = d;
                }

            BuildCoastField();

            _g = new float[W * H];
            _from = new int[W * H];
            _stamp = new int[W * H];
            _heap = new int[W * H];
            _heapKey = new float[W * H];
            _heapIndex = new int[W * H];
        }

        void BuildCoastField()
        {
            // multi-source BFS out of land cells (chebyshev-ish distance in cells)
            var q = new Queue<int>();
            for (int i = 0; i < _depth.Length; i++)
            {
                if (_depth[i] <= 0.001f) { _coast[i] = 0; q.Enqueue(i); }
                else _coast[i] = 255;
            }
            while (q.Count > 0)
            {
                int i = q.Dequeue();
                int x = i % W, y = i / W;
                byte nd = (byte)Mathf.Min(255, _coast[i] + 1);
                if (nd >= 255) continue;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                        int ni = ny * W + nx;
                        if (_coast[ni] > nd) { _coast[ni] = nd; q.Enqueue(ni); }
                    }
            }
        }

        // ------------------------------------------------------------------ helpers

        public Vector2 CellCenter(int x, int y)
        {
            float half = GameConfig.WorldSize * 0.5f;
            return new Vector2(-half + (x + 0.5f) * CellSize, -half + (y + 0.5f) * CellSize);
        }

        public void WorldToCell(Vector2 w, out int x, out int y)
        {
            float half = GameConfig.WorldSize * 0.5f;
            x = Mathf.Clamp(Mathf.FloorToInt((w.x + half) / CellSize), 0, W - 1);
            y = Mathf.Clamp(Mathf.FloorToInt((w.y + half) / CellSize), 0, H - 1);
        }

        public float CellDepth(int x, int y) => _depth[y * W + x];
        public int CoastDistance(int x, int y) => _coast[y * W + x];

        public bool Passable(int x, int y, float draft)
            => _depth[y * W + x] >= draft * WorldMap.DraftToDepth;

        public bool PassableWorld(Vector2 w, float draft)
        {
            WorldToCell(w, out int x, out int y);
            return Passable(x, y, draft);
        }

        /// <summary>Straight-line navigability test, sampled along the segment.</summary>
        public bool LineOfWater(Vector2 a, Vector2 b, float draft)
        {
            float dist = Vector2.Distance(a, b);
            int steps = Mathf.Max(2, Mathf.CeilToInt(dist / (CellSize * 0.6f)));
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
                if (!PassableWorld(p, draft)) return false;
            }
            return true;
        }

        public Vector2 NearestNavigable(Vector2 w, float draft, int maxRings = 30)
        {
            if (PassableWorld(w, draft)) return w;
            WorldToCell(w, out int cx, out int cy);
            for (int r = 1; r <= maxRings; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                        int x = cx + dx, y = cy + dy;
                        if (x < 0 || y < 0 || x >= W || y >= H) continue;
                        if (Passable(x, y, draft)) return CellCenter(x, y);
                    }
            }
            return w;
        }

        // ------------------------------------------------------------------ requests

        public object RequestPath(Vector2 start, Vector2 goal, float draft, Action<List<Vector2>> callback)
        {
            var r = new Request { start = start, goal = goal, draft = draft, callback = callback };
            _queue.Enqueue(r);
            return r;
        }

        public void CancelRequest(object handle)
        {
            if (handle is Request r) r.cancelled = true;
        }

        void Update()
        {
            SolvesThisFrame = 0;
            while (_queue.Count > 0 && SolvesThisFrame < GameConfig.PathThrottlePerFrame)
            {
                var r = _queue.Dequeue();
                if (r.cancelled) continue;
                SolvesThisFrame++;
                var path = FindPath(r.start, r.goal, r.draft);
                r.callback?.Invoke(path);
            }
        }

        // ------------------------------------------------------------------ A*

        public List<Vector2> FindPath(Vector2 startW, Vector2 goalW, float draft, int maxNodes = 24000)
        {
            if (_depth == null) return null;

            Vector2 s = NearestNavigable(startW, draft);
            Vector2 g = NearestNavigable(goalW, draft);

            WorldToCell(s, out int sx, out int sy);
            WorldToCell(g, out int gx, out int gy);
            int startIdx = sy * W + sx, goalIdx = gy * W + gx;

            if (startIdx == goalIdx)
                return new List<Vector2> { goalW };

            // trivial case: clear water straight to the target
            if (LineOfWater(startW, goalW, draft))
                return new List<Vector2> { goalW };

            _currentStamp++;
            _heapCount = 0;

            _g[startIdx] = 0f;
            _from[startIdx] = -1;
            _stamp[startIdx] = _currentStamp;
            HeapPush(startIdx, Heuristic(sx, sy, gx, gy));

            int expanded = 0;
            bool found = false;

            while (_heapCount > 0 && expanded < maxNodes)
            {
                int cur = HeapPop();
                if (cur == goalIdx) { found = true; break; }
                expanded++;

                int cx = cur % W, cy = cur / W;
                float gc = _g[cur];

                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = cx + dx, ny = cy + dy;
                        if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                        if (!Passable(nx, ny, draft)) continue;
                        // no cutting diagonally past a headland
                        if (dx != 0 && dy != 0 && (!Passable(cx + dx, cy, draft) || !Passable(cx, cy + dy, draft))) continue;

                        int ni = ny * W + nx;
                        float step = (dx != 0 && dy != 0) ? 1.41421f : 1f;

                        // keep a respectful distance from the beach
                        int coast = _coast[ni];
                        float shorePenalty = coast < 3 ? (3 - coast) * 1.9f : 0f;
                        // and a mild preference for deeper water
                        float depthPenalty = Mathf.Clamp01(0.35f - _depth[ni]) * 2.2f;

                        float ng = gc + step + shorePenalty + depthPenalty;

                        if (_stamp[ni] != _currentStamp)
                        {
                            _stamp[ni] = _currentStamp;
                            _g[ni] = ng;
                            _from[ni] = cur;
                            HeapPush(ni, ng + Heuristic(nx, ny, gx, gy));
                        }
                        else if (ng < _g[ni])
                        {
                            _g[ni] = ng;
                            _from[ni] = cur;
                            HeapPushOrUpdate(ni, ng + Heuristic(nx, ny, gx, gy));
                        }
                    }
            }

            if (!found)
            {
                // fall back to the closest reachable node we expanded
                int best = -1; float bestH = float.MaxValue;
                for (int i = 0; i < _stamp.Length; i++)
                {
                    if (_stamp[i] != _currentStamp) continue;
                    int x = i % W, y = i / W;
                    float h = Heuristic(x, y, gx, gy);
                    if (h < bestH) { bestH = h; best = i; }
                }
                if (best < 0) return null;
                goalIdx = best;
            }

            // reconstruct
            var cells = new List<int>();
            int node = goalIdx;
            int guard = 0;
            while (node >= 0 && guard++ < 100000)
            {
                cells.Add(node);
                node = _from[node];
            }
            cells.Reverse();

            var pts = new List<Vector2>(cells.Count);
            for (int i = 0; i < cells.Count; i++)
                pts.Add(CellCenter(cells[i] % W, cells[i] / W));
            if (found) pts[pts.Count - 1] = goalW;

            return Smooth(pts, draft);
        }

        float Heuristic(int x, int y, int gx, int gy)
        {
            float dx = Mathf.Abs(x - gx), dy = Mathf.Abs(y - gy);
            return (dx + dy) + (1.41421f - 2f) * Mathf.Min(dx, dy);
        }

        /// <summary>String pulling: drop waypoints that are visible from the previous kept one.</summary>
        List<Vector2> Smooth(List<Vector2> pts, float draft)
        {
            if (pts == null || pts.Count <= 2) return pts;
            var outPts = new List<Vector2>(pts.Count) { pts[0] };
            int i = 0;
            while (i < pts.Count - 1)
            {
                int j = pts.Count - 1;
                for (; j > i + 1; j--)
                    if (LineOfWater(pts[i], pts[j], draft)) break;
                outPts.Add(pts[j]);
                i = j;
            }
            if (outPts.Count > 1) outPts.RemoveAt(0);   // first point is the ship's own cell
            return outPts;
        }

        // ------------------------------------------------------------------ binary heap

        void HeapPush(int node, float key)
        {
            int i = _heapCount++;
            _heap[i] = node; _heapKey[i] = key; _heapIndex[node] = i;
            HeapUp(i);
        }

        void HeapPushOrUpdate(int node, float key)
        {
            int i = _heapIndex[node];
            if (i >= 0 && i < _heapCount && _heap[i] == node)
            {
                _heapKey[i] = key;
                HeapUp(i);
            }
            else HeapPush(node, key);
        }

        int HeapPop()
        {
            int top = _heap[0];
            _heapCount--;
            if (_heapCount > 0)
            {
                _heap[0] = _heap[_heapCount];
                _heapKey[0] = _heapKey[_heapCount];
                _heapIndex[_heap[0]] = 0;
                HeapDown(0);
            }
            _heapIndex[top] = -1;
            return top;
        }

        void HeapUp(int i)
        {
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (_heapKey[p] <= _heapKey[i]) break;
                Swap(i, p); i = p;
            }
        }

        void HeapDown(int i)
        {
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, m = i;
                if (l < _heapCount && _heapKey[l] < _heapKey[m]) m = l;
                if (r < _heapCount && _heapKey[r] < _heapKey[m]) m = r;
                if (m == i) break;
                Swap(i, m); i = m;
            }
        }

        void Swap(int a, int b)
        {
            int n = _heap[a]; _heap[a] = _heap[b]; _heap[b] = n;
            float k = _heapKey[a]; _heapKey[a] = _heapKey[b]; _heapKey[b] = k;
            _heapIndex[_heap[a]] = a; _heapIndex[_heap[b]] = b;
        }
    }
}
