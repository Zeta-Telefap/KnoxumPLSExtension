using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace LifterFrenzy
{
    /// <summary>
    /// Lockers: the duplicated back face and the cells a locker covers.
    /// </summary>
    public sealed partial class PropLifter
    {


        private static bool IsLocker(Transform root)
        {
            return root != null && root.name.ToLowerInvariant().Contains("locker");
        }

        // The name of the copy that gives a locker its back (it must stay free of the words IsSupportedPropName and
        // ForbiddenNameParts look for).
        private const string LockerBackName = "BackFace";

        // The copy is a flat slab at the open back: this thick (a share of the locker's depth), this far inside
        // the back edge (units), and a hair smaller in height and width than the locker, so the edges of the
        // real sides and the top cover it.
        private const float LockerBackThickness = 0.02f;
        private const float LockerBackGap = 0.003f;
        private const float LockerBackScale = 0.998f;

        /// <summary>
        /// The red and blue lockers (LockerBuilder: lockerPre and hideLockerPre) have no back; the game never shows
        /// it because they stand against a wall. A carried locker shows it, so one of its sides is copied onto the
        /// back: a copy of every mesh of the locker, turned a quarter circle around the vertical axis through its
        /// middle, squeezed along the locker's width until it is a flat slab and stretched along its depth until
        /// that is as wide as the locker, then moved to the open back. The side of the copy that was a side of the
        /// locker now covers the back (the sides keep their texture and the way up); the rest of the copy is flat
        /// and lies inside the locker. The locker's width runs along its local X (the lockers of a row stand 2
        /// units apart along X) and the open back is toward +Z, the wall (LockerBuilder turns the locker with its
        /// +Z toward the wall). The copy is made once, stays part of the locker and is listed in the cell like
        /// the locker's own renderers, so the chunk culling shows and hides it with them. Returns every renderer
        /// of the locker, the copy included.
        /// </summary>
        private static Renderer[] EnsureLockerBack(Cell cell, Transform root, Renderer[] renderers)
        {
            try
            {
                if (root.Find(LockerBackName) != null)
                {
                    return root.GetComponentsInChildren<Renderer>(true);
                }

                // The middle of the locker in its own coordinates, from the meshes (a culled renderer has no bounds).
                Bounds local = new Bounds();
                bool hasBounds = false;
                List<MeshRenderer> sources = new List<MeshRenderer>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    MeshRenderer source = renderers[i] as MeshRenderer;
                    Bounds mesh;
                    if (source == null || !TryGetLocalMeshBounds(source, out mesh))
                    {
                        continue;
                    }

                    sources.Add(source);
                    for (int corner = 0; corner < 8; corner++)
                    {
                        Vector3 point = mesh.center + new Vector3(
                            (corner & 1) == 0 ? -mesh.extents.x : mesh.extents.x,
                            (corner & 2) == 0 ? -mesh.extents.y : mesh.extents.y,
                            (corner & 4) == 0 ? -mesh.extents.z : mesh.extents.z);
                        point = root.InverseTransformPoint(source.transform.TransformPoint(point));
                        if (hasBounds)
                        {
                            local.Encapsulate(point);
                        }
                        else
                        {
                            local = new Bounds(point, Vector3.zero);
                            hasBounds = true;
                        }
                    }
                }

                float halfWidth = local.extents.x;
                float halfDepth = local.extents.z;
                if (!hasBounds || halfWidth < 0.05f || halfDepth < 0.05f)
                {
                    return renderers;
                }

                // p -> slabCentre + Q * S * (p - c), written as the pose of the holder (the copies keep their own
                // pose relative to the locker, which the holder then turns and squeezes). Q is a quarter turn
                // around the vertical: the copy's left side (-X) lands on the back plane (+Z), turned to face
                // away from the locker, its depth axis along X.
                Vector3 c = local.center;
                Quaternion turn = Quaternion.Euler(0f, 90f, 0f);
                Vector3 squeeze = new Vector3(
                    LockerBackThickness * halfDepth / halfWidth,
                    LockerBackScale,
                    LockerBackScale * halfWidth / halfDepth);
                Vector3 slabCentre = new Vector3(
                    c.x, c.y, local.max.z - LockerBackThickness * halfDepth - LockerBackGap);
                Transform holder = new GameObject(LockerBackName).transform;
                holder.SetParent(root, false);
                holder.localRotation = turn;
                holder.localScale = squeeze;
                holder.localPosition = slabCentre - turn * Vector3.Scale(squeeze, c);

                Vector3 rootScale = root.lossyScale;
                List<Renderer> copies = new List<Renderer>();
                for (int i = 0; i < sources.Count; i++)
                {
                    MeshRenderer source = sources[i];
                    MeshFilter filter = source.GetComponent<MeshFilter>();
                    Vector3 sourceScale = source.transform.lossyScale;
                    GameObject part = new GameObject(LockerBackName + " Mesh");
                    part.layer = source.gameObject.layer;
                    Transform partTransform = part.transform;
                    partTransform.SetParent(holder, false);
                    partTransform.localPosition = root.InverseTransformPoint(source.transform.position);
                    partTransform.localRotation = Quaternion.Inverse(root.rotation) * source.transform.rotation;
                    partTransform.localScale = new Vector3(
                        rootScale.x != 0f ? sourceScale.x / rootScale.x : sourceScale.x,
                        rootScale.y != 0f ? sourceScale.y / rootScale.y : sourceScale.y,
                        rootScale.z != 0f ? sourceScale.z / rootScale.z : sourceScale.z);
                    part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    MeshRenderer copy = part.AddComponent<MeshRenderer>();
                    copy.sharedMaterials = source.sharedMaterials;
                    copy.shadowCastingMode = source.shadowCastingMode;
                    copy.receiveShadows = source.receiveShadows;
                    copies.Add(copy);
                }

                AddRendererReferences(cell, copies);
                Debug.Log("[Lifter Frenzy] " + root.name + " got a back: a side of the locker copied onto it ("
                    + copies.Count + " mesh copies, locker box " + local.size.x.ToString("0.00") + " wide, "
                    + local.size.y.ToString("0.00") + " high, " + local.size.z.ToString("0.00") + " deep).");
                return root.GetComponentsInChildren<Renderer>(true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Lifter Frenzy] The locker back could not be made: " + exception.Message);
                return renderers;
            }
        }

        private static CellCoverage LockerCoverage(Quaternion rotation)
        {
            Direction wallDirection = DirectionFromRotation(rotation);
            return CellCoverage.Down
                | (wallDirection == Direction.Null ? CellCoverage.None : wallDirection.ToCoverage());
        }

        private static bool IsChildOfCellObjectBase(Transform root, Cell cell)
        {
            return root != null && cell != null && cell.ObjectBase != null
                && (root == cell.ObjectBase || root.IsChildOf(cell.ObjectBase));
        }
    }
}
