using System.Collections.Generic;
using Meta.XR.MRUtilityKit;
using UnityEngine;

namespace MRStainCleaning.MRUKFloor
{
    public readonly struct FloorPlaneData
    {
        public FloorPlaneData(
            MRUKRoom room,
            MRUKAnchor floorAnchor,
            Vector3 centerWorld,
            Quaternion rotationWorld,
            Vector3 normalWorld,
            float width,
            float height,
            IReadOnlyList<Vector2> boundaryLocal2D)
        {
            Room = room;
            FloorAnchor = floorAnchor;
            CenterWorld = centerWorld;
            RotationWorld = rotationWorld;
            NormalWorld = normalWorld;
            Width = width;
            Height = height;
            BoundaryLocal2D = boundaryLocal2D;
        }

        public MRUKRoom Room { get; }
        public MRUKAnchor FloorAnchor { get; }
        public Vector3 CenterWorld { get; }
        public Quaternion RotationWorld { get; }
        public Vector3 NormalWorld { get; }
        public float Width { get; }
        public float Height { get; }
        public IReadOnlyList<Vector2> BoundaryLocal2D { get; }
        public float Area => Width * Height;
    }
}
