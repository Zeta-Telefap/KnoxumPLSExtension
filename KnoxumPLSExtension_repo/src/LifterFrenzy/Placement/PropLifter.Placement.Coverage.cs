using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// What a cell covers: the renderer lists the game keeps per object (their order is the render order) and the
    /// coverage flags a prop adds to its cell.
    /// </summary>
    public sealed partial class PropLifter
    {


        /// <summary>
        /// The renderers of the prop that some cell lists: the game's culling shows and hides the listed
        /// renderers with their chunk. The level builder and CullingManager.PrepareOcclusionCalculations both
        /// list an object's renderers, so one renderer can be listed twice, or in the list of another cell.
        /// </summary>
        private static List<Renderer> RegisteredRenderers(EnvironmentController environment, Renderer[] renderers)
        {
            List<Renderer> registered = new List<Renderer>();
            if (renderers == null)
            {
                return registered;
            }

            HashSet<Renderer> wanted = new HashSet<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    wanted.Add(renderers[i]);
                }
            }

            HashSet<Renderer> found = new HashSet<Renderer>();
            ScanRendererLists(environment, wanted, found, false);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && found.Contains(renderers[i]) && !registered.Contains(renderers[i]))
                {
                    registered.Add(renderers[i]);
                }
            }

            return registered;
        }

        private static void MoveRendererReferences(Cell from, Cell to, List<Renderer> renderers)
        {
            RemoveRendererReferences(from, renderers);
            AddRendererReferences(to, renderers);
        }

        /// <summary>Takes every listing of these renderers out of one cell's list (not just the first).</summary>
        private static void RemoveRendererReferences(Cell cell, List<Renderer> renderers)
        {
            if (renderers == null || renderers.Count == 0)
            {
                return;
            }

            ScanRendererList(cell, new HashSet<Renderer>(renderers), null, true);
        }

        /// <summary>
        /// Takes every listing of these renderers out of every cell. A listing left behind in the cell the prop
        /// came from would let that chunk's culling hide the model wherever the prop stands now.
        /// </summary>
        private static void RemoveRendererReferencesEverywhere(
            EnvironmentController environment, List<Renderer> renderers)
        {
            if (renderers == null || renderers.Count == 0)
            {
                return;
            }

            ScanRendererLists(environment, new HashSet<Renderer>(renderers), null, true);
        }

        /// <summary>
        /// Lists the renderers in the cell again. The culling only touches a listed renderer when its chunk
        /// changes between shown and hidden, so a prop put into a chunk takes that chunk's state at once.
        /// </summary>
        private static void AddRendererReferences(Cell cell, List<Renderer> renderers)
        {
            if (cell == null || cell.renderers == null || renderers == null)
            {
                return;
            }

            bool chunkShown = !cell.HasChunk || cell.Chunk == null || cell.Chunk.Rendering;
            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!cell.renderers.Contains(renderer))
                {
                    cell.renderers.Add(renderer);
                }

                renderer.enabled = chunkShown;
            }
        }

        /// <summary>
        /// Walks the renderer list of every cell: the renderers in `wanted` are reported in `found` and, with
        /// `remove`, taken out of the list (every occurrence; destroyed renderers go with them).
        /// </summary>
        private static void ScanRendererLists(
            EnvironmentController environment, HashSet<Renderer> wanted, HashSet<Renderer> found, bool remove)
        {
            if (environment == null || environment.cells == null || wanted.Count == 0)
            {
                return;
            }

            Cell[,] cells = environment.cells;
            int width = cells.GetLength(0);
            int depth = cells.GetLength(1);
            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < depth; z++)
                {
                    ScanRendererList(cells[x, z], wanted, found, remove);
                }
            }
        }

        private static void ScanRendererList(
            Cell cell, HashSet<Renderer> wanted, HashSet<Renderer> found, bool remove)
        {
            if (cell == null || cell.renderers == null)
            {
                return;
            }

            List<Renderer> listed = cell.renderers;
            for (int i = listed.Count - 1; i >= 0; i--)
            {
                Renderer renderer = listed[i];
                if (renderer == null)
                {
                    if (remove)
                    {
                        // Chunk.Render would throw on it and skip the rest of the list.
                        listed.RemoveAt(i);
                    }

                    continue;
                }

                if (!wanted.Contains(renderer))
                {
                    continue;
                }

                if (found != null)
                {
                    found.Add(renderer);
                }

                if (remove)
                {
                    listed.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// The coverage that the other props standing in this cell still provide: the EnvironmentObjects of the
        /// cell's room and the lockers on the cell's base. Only those two places are looked at (they are where such
        /// props live); asking Unity for every EnvironmentObject and every MeshRenderer of the scene on each pickup
        /// froze the game for a while on floors with many objects.
        /// </summary>
        private static CellCoverage OtherCoverageProviders(
            EnvironmentController environment,
            Cell cell,
            Transform excludedRoot,
            CellCoverage coverage)
        {
            CellCoverage provided = CellCoverage.None;
            if (cell == null || cell.room == null)
            {
                return provided;
            }

            if (cell.room.objectObject != null)
            {
                EnvironmentObject[] objects = cell.room.objectObject.GetComponentsInChildren<EnvironmentObject>(true);
                for (int i = 0; i < objects.Length; i++)
                {
                    EnvironmentObject item = objects[i];
                    if (item == null || item.transform == excludedRoot || item.Ec != environment)
                    {
                        continue;
                    }

                    Cell itemCell;
                    if (TryGetCell(environment, item.transform.position, out itemCell) && itemCell == cell)
                    {
                        provided |= item.coverage.Rotated(DirectionFromRotation(item.transform.rotation));
                    }
                }
            }

            if (cell.HasObjectBase)
            {
                MeshRenderer[] renderers = cell.ObjectBase.GetComponentsInChildren<MeshRenderer>(true);
                HashSet<Transform> visited = new HashSet<Transform>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    Transform root = renderers[i] != null
                        ? FindSupportedPropRoot(renderers[i].transform) : null;
                    if (root == null || root == excludedRoot || !IsLocker(root) || !visited.Add(root))
                    {
                        continue;
                    }

                    Cell itemCell;
                    if (TryGetCell(environment, root.position, out itemCell) && itemCell == cell)
                    {
                        provided |= LockerCoverage(root.rotation);
                    }
                }
            }

            return provided & coverage;
        }

        private static CellCoverage ReadCoverage(FieldInfo field, Cell cell)
        {
            if (field == null || cell == null)
            {
                throw new InvalidOperationException("Cell coverage data is unavailable.");
            }

            return (CellCoverage)field.GetValue(cell);
        }

        private static void WriteCoverage(Cell cell, CellCoverage hard, CellCoverage soft)
        {
            if (cell == null || HardCoverageField == null || SoftCoverageField == null)
            {
                throw new InvalidOperationException("Cell coverage fields are unavailable.");
            }

            HardCoverageField.SetValue(cell, hard);
            SoftCoverageField.SetValue(cell, soft);
        }
    }
}
