using System.Collections.Generic;
using UnityEngine;

namespace PaletteRoom
{
    /// <summary>
    /// The sample room's initial layout, fitted to the scanned room with common interior-design spacing:
    /// the sofa backs onto a solid wall with art hung above it, the coffee table sits 40 cm in front of
    /// the sofa on a rug that catches the sofa's front legs, a side table and lamp flank the sofa, an
    /// armchair angles in at the end of the coffee table, and the bookshelf and plant share out the rest
    /// of the wall with walkways of at least 76 cm. If something doesn't fit, smaller sizes are tried and
    /// then the least important pieces are left out.
    /// </summary>
    public partial class RoomFurniture
    {
        // Spacing rules of thumb (metres).
        const float SofaToWall = 0.08f;        // a few cm so the sofa doesn't scuff the wall
        const float SofaToCoffeeTable = 0.40f; // 14–18 in
        const float SideTableToSofa = 0.07f;   // 2–4 in
        const float Walkway = 0.76f;           // 30 in minimum; 36 in when there's room
        const float RugUnderSofa = 0.25f;      // front legs on the rug
        const float ArtAboveFurniture = 0.22f; // 8–10 in from the furniture's top to the frame
        const float ArtCentreHeight = 1.45f;   // 57 in, when nothing is below the art
        const float MaxWallDistance = 5.5f;
        const float MaxBedSide = 3.2f;         // a bed outline bigger than this has merged with something else

        /// <summary>A place to build the room from: a line on the floor (a wall) and the direction into the room.</summary>
        struct Frame
        {
            public Vector3 origin;  // on the wall's floor line
            public Vector3 along;   // along the wall
            public Vector3 inward;  // away from the wall, into the room
            public float halfLength;
            public float wallTop;   // metres above the floor the wall reaches
            public bool real;       // false: no scanned wall, a virtual one in front of the user

            public Vector3 Point(float x, float z, float floorY)
            {
                var p = origin + along * x + inward * z;
                return new Vector3(p.x, floorY, p.z);
            }

            public Quaternion Facing(float yawOffset = 0f) =>
                Quaternion.LookRotation(Quaternion.Euler(0f, yawOffset, 0f) * inward, Vector3.up);
        }

        /// <summary>A piece's collision box in its own space, for placing it by its edges.</summary>
        static Bounds LocalBox(FurnitureItem item)
        {
            var boxes = item.GetComponents<BoxCollider>();
            if (boxes.Length == 0)
                return new Bounds(Vector3.zero, Vector3.one * 0.5f);
            var b = new Bounds(boxes[0].center, boxes[0].size);
            for (int i = 1; i < boxes.Length; i++)
                b.Encapsulate(new Bounds(boxes[i].center, boxes[i].size));
            return b;
        }

        IEnumerable<Frame> CandidateFrames(Transform head, float floorY)
        {
            var user = new Vector2(head.position.x, head.position.z);
            var forward = FlatForward(head);
            var frames = new List<(float score, Frame frame)>();
            if (sceneObstacles != null)
            {
                foreach (var wall in sceneObstacles.SolidWalls)
                {
                    // The wall's horizontal axis runs along it; its normal points into the room (toward the user).
                    var x = wall.rotation * Vector3.right;
                    var y = wall.rotation * Vector3.up;
                    bool xHorizontal = Mathf.Abs(x.y) <= Mathf.Abs(y.y);
                    var along = Vector3.ProjectOnPlane(xHorizontal ? x : y, Vector3.up).normalized;
                    float length = xHorizontal ? wall.size.x : wall.size.y;
                    float height = xHorizontal ? wall.size.y : wall.size.x;
                    if (length < 1.6f || along.sqrMagnitude < 0.5f)
                        continue;
                    var inward = Vector3.Cross(Vector3.up, along);
                    var centre = new Vector3(wall.centre.x, floorY, wall.centre.z);
                    var toUser = new Vector3(user.x - centre.x, 0f, user.y - centre.z);
                    if (Vector3.Dot(toUser, inward) < 0f)
                        inward = -inward;
                    float distance = Vector3.Dot(toUser, inward);
                    // The seating group runs about 2 m out from the wall, and the user needs a walkway beyond it.
                    if (distance < 2.4f || distance > MaxWallDistance)
                        continue;
                    // Prefer walls the user faces, then longer walls, then ones at a comfortable 3 m or so.
                    float facing = Vector3.Dot(-inward, forward);
                    float score = facing * 2f + Mathf.Min(length, 5f) * 0.3f - Mathf.Abs(distance - 3f) * 0.3f;
                    float top = wall.centre.y + height / 2f - floorY;
                    frames.Add((score, new Frame { origin = centre, along = along, inward = inward, halfLength = length / 2f, wallTop = top, real = true }));
                }
            }
            frames.Sort((a, b) => b.score.CompareTo(a.score));
            foreach (var f in frames)
                yield return f.frame;

            // No usable wall (e.g. Editor testing): a virtual wall behind where the sofa would stand,
            // drawn in closer when a real wall is in the way so the room stays on the user's side of it.
            foreach (float reach in new[] { distanceFromUser + 1.2f, distanceFromUser + 0.6f, distanceFromUser })
            {
                var virtualOrigin = head.position + forward * reach;
                virtualOrigin.y = floorY;
                if (BehindWall(new Vector3(user.x, floorY, user.y), virtualOrigin))
                    continue;
                yield return new Frame
                {
                    origin = virtualOrigin,
                    along = Vector3.Cross(forward, Vector3.up) * -1f,
                    inward = -forward,
                    halfLength = 3f,
                    wallTop = 2.4f,
                    real = false,
                };
            }
        }

        /// <summary>Builds the sample room against the best wall it fits; returns the pieces placed.</summary>
        List<FurnitureItem> LayOutSampleRoom(Transform root, Transform head, float floorY)
        {
            m_UserSpot = new Vector2(head.position.x, head.position.z);
            var placed = new List<FurnitureItem>();
            foreach (var frame in CandidateFrames(head, floorY))
            {
                var layout = TryLayOut(root, frame, head, floorY);
                if (layout != null)
                {
                    Debug.Log($"[PaletteRoom] Sample room laid out against {(frame.real ? "a scanned wall" : "a virtual wall")}: {layout.Count} pieces.");
                    placed = layout;
                    break;
                }
            }
            if (placed.Count == 0)
                Debug.LogWarning("[PaletteRoom] No room for a sofa and coffee table anywhere nearby.");
            DressRealFurniture(root, floorY, placed);
            return placed;
        }

        /// <summary>
        /// Works the palette into the furniture already in the room: pillows stand against the head of
        /// the bed, and a pillow sits in the back corner of each real sofa or armchair.
        /// </summary>
        void DressRealFurniture(Transform root, float floorY, List<FurnitureItem> placed)
        {
            if (sceneObstacles == null)
                return;
            RoomSurface? bed = null;
            float bedArea = 0f;
            var seats = new List<RoomSurface>();
            foreach (var (s, height) in sceneObstacles.FurnitureTops)
            {
                float area = s.size.x * s.size.y;
                bool labelledBed = s.label == "Bed";
                bool bedLike = labelledBed || (area >= 1.8f && height >= 0.25f && height <= 0.8f && s.label != "Table");
                if (bedLike && height >= 0.25f && height <= 0.9f && area > bedArea)
                {
                    bed = s;
                    bedArea = area;
                }
                else if ((s.label == "Sofa" || s.label == "Chair") && height >= 0.3f && height <= 0.7f && area >= 0.2f && !bedLike)
                {
                    seats.Add(s);
                }
            }

            if (bed.HasValue)
            {
                var b = bed.Value;
                // Scans often cover only part of the mattress (e.g. the duvet at the foot), so the bed's
                // outline is taken from the whole merged obstacle (platform + mattress), whose head edge
                // reaches the wall. The head is that outline's edge nearest a wall.
                var outline = BedOutline(b);
                var centre = new Vector3(outline.centre.x, b.centre.y, outline.centre.y);
                var ax = new Vector3(outline.axisX.x, 0f, outline.axisX.y);
                var az = new Vector3(outline.axisZ.x, 0f, outline.axisZ.y);
                Vector3 head = ax, across = az;
                float halfDepth = outline.half.x, edgeLength = outline.half.y * 2f, wallDistance = float.MaxValue;
                foreach (var (dir, half, side, sideLength) in new[]
                {
                    (ax, outline.half.x, az, outline.half.y * 2f), (-ax, outline.half.x, az, outline.half.y * 2f),
                    (az, outline.half.y, ax, outline.half.x * 2f), (-az, outline.half.y, ax, outline.half.x * 2f),
                })
                {
                    float d = DistanceToWall(centre + dir * half);
                    if (d < wallDistance)
                    {
                        wallDistance = d; head = dir; halfDepth = half; across = side; edgeLength = sideLength;
                    }
                }
                // Up at the top with the bed's own pillows: about 30 cm in from the headboard edge,
                // resting a little higher than the mattress so they sit on the pillow row.
                const float fromHeadboard = 0.3f, onPillowRow = 0.08f;
                var row = centre + head * (halfDepth - fromHeadboard);
                var toFoot = -head;
                float spread = Mathf.Min(0.35f, edgeLength * 0.2f);
                int count = edgeLength >= 1.3f ? 3 : 2;
                for (int i = 0; i < count; i++)
                {
                    bool lumbar = count == 3 && i == 2;
                    float offset = lumbar ? 0f : (i == 0 ? -1f : 1f) * spread;
                    var pillow = Make(root, FurnitureKind.ThrowPillow, lumbar ? "lumbar" : "large");
                    var p = row + across * offset + toFoot * (lumbar ? 0.2f : 0f);
                    pillow.transform.SetPositionAndRotation(new Vector3(p.x, b.centre.y + (lumbar ? 0.02f : onPillowRow), p.z),
                        Quaternion.LookRotation(toFoot, Vector3.up) * Quaternion.Euler(-18f, 0f, 0f));
                    placed.Add(pillow);
                }
                Debug.Log($"[PaletteRoom] Bed ({b.label}, top {b.centre.y - floorY:F2} m above the floor, outline {outline.half.x * 2f:F2}×{outline.half.y * 2f:F2} m): headboard edge {wallDistance:F2} m from a wall.");
            }

            foreach (var seat in seats)
            {
                Axes(seat, out var lengthAxis, out float length, out var widthAxis, out float width);
                // The back is the long edge nearer a wall.
                var sideA = seat.centre + widthAxis * (width / 2f);
                var sideB = seat.centre - widthAxis * (width / 2f);
                var back = DistanceToWall(sideA) <= DistanceToWall(sideB) ? widthAxis : -widthAxis;
                var pillow = Make(root, FurnitureKind.ThrowPillow, "square");
                var p = seat.centre + back * (width / 2f - 0.15f) + lengthAxis * (length / 2f - 0.35f);
                pillow.transform.SetPositionAndRotation(new Vector3(p.x, seat.centre.y, p.z),
                    Quaternion.LookRotation(-back, Vector3.up) * Quaternion.Euler(-15f, 0f, 0f));
                placed.Add(pillow);
            }
            if (bed.HasValue || seats.Count > 0)
                Debug.Log($"[PaletteRoom] Dressed the real room: {(bed.HasValue ? "pillows on the bed" : "no bed")}, {seats.Count} seats.");
        }

        /// <summary>The merged real-furniture obstacle the bed surface belongs to (falls back to the surface itself).</summary>
        Footprint BedOutline(RoomSurface bed)
        {
            var c = new Vector2(bed.centre.x, bed.centre.z);
            var point = new[] { new Footprint(c, Vector2.right, Vector2.up, Vector2.one * 0.01f) };
            foreach (var f in sceneObstacles.Furniture)
                if (f.half.x * 2f <= MaxBedSide && f.half.y * 2f <= MaxBedSide && FurnitureItem.Overlap(new[] { f }, point))
                    return f;
            var x = Vector3.ProjectOnPlane(bed.rotation * Vector3.right, Vector3.up).normalized;
            var y = Vector3.ProjectOnPlane(bed.rotation * Vector3.up, Vector3.up).normalized;
            return new Footprint(c, new Vector2(x.x, x.z), new Vector2(y.x, y.z), bed.size * 0.5f);
        }

        /// <summary>A horizontal surface's long and short axes on the floor plane, with their lengths.</summary>
        static void Axes(RoomSurface s, out Vector3 lengthAxis, out float length, out Vector3 widthAxis, out float width)
        {
            var x = Vector3.ProjectOnPlane(s.rotation * Vector3.right, Vector3.up).normalized;
            var y = Vector3.ProjectOnPlane(s.rotation * Vector3.up, Vector3.up).normalized;
            if (s.size.x >= s.size.y)
            {
                lengthAxis = x; length = s.size.x; widthAxis = y; width = s.size.y;
            }
            else
            {
                lengthAxis = y; length = s.size.y; widthAxis = x; width = s.size.x;
            }
        }

        float DistanceToWall(Vector3 point)
        {
            float best = float.MaxValue;
            if (sceneObstacles == null)
                return best;
            var p = new Vector2(point.x, point.z);
            foreach (var w in sceneObstacles.Walls)
            {
                var local = p - w.centre;
                float along = Mathf.Clamp(Vector2.Dot(local, w.axisX), -w.half.x, w.half.x);
                var closest = w.centre + w.axisX * along;
                best = Mathf.Min(best, Vector2.Distance(p, closest));
            }
            return best;
        }

        FurnitureItem Make(Transform root, FurnitureKind kind, string variant)
        {
            var item = Factory.Create(kind, FurnitureCatalog.Get(kind).Variant(variant), root);
            m_Items.Add(item); // so later pieces test against it
            return item;
        }

        void Discard(FurnitureItem item)
        {
            if (item == null)
                return;
            m_Items.Remove(item);
            Destroy(item.gameObject);
        }

        /// <summary>Places <paramref name="item"/> at a frame position (its back edge <paramref name="backGap"/> from the wall) if it's free.</summary>
        bool Put(FurnitureItem item, Frame f, float x, float backGap, float floorY, float yaw = 0f, float clearance = 0f)
        {
            var box = LocalBox(item);
            var rotation = f.Facing(yaw);
            // With yaw 0, the item's back (-Z) faces the wall.
            float z = backGap - box.min.z;
            var position = f.Point(x, z, floorY);
            if (!Fits(item, position, rotation, clearance, f))
                return false;
            item.transform.SetPositionAndRotation(position, rotation);
            return true;
        }

        /// <summary>Free of walls, real furniture and other pieces (keeping <paramref name="clearance"/> from pieces), and on this side of the wall.</summary>
        bool Fits(FurnitureItem item, Vector3 position, Quaternion rotation, float clearance, Frame f)
        {
            if (f.real && Mathf.Abs(Vector3.Dot(position - f.origin, f.along)) > f.halfLength + 0.5f)
                return false; // well past the end of the wall: probably another room
            if (BehindWall(new Vector3(m_UserSpot.x, position.y, m_UserSpot.y), position))
                return false; // through a real wall: the passthrough occluder would hide it
            if (item.Kind != FurnitureKind.Rug && TooCloseToUser(item.FootprintsAt(position, rotation)))
                return false;
            if (!item.BlocksFloor)
                return ClearOfRoom(item.FootprintsAt(position, rotation));
            if (!IsFree(item, position, rotation))
                return false;
            if (clearance <= 0f)
                return true;
            var grown = Grow(item.FootprintsAt(position, rotation), clearance);
            foreach (var other in m_Items)
            {
                if (other == null || other == item || !other.BlocksFloor)
                    continue;
                if (FurnitureItem.Overlap(grown, other.CurrentFootprints))
                    return false;
            }
            return true;
        }

        Vector2 m_UserSpot;

        /// <summary>Nothing is set down on or right next to the user: they keep a walkway's worth of space.</summary>
        bool TooCloseToUser(Footprint[] footprints)
        {
            var user = new[] { new Footprint(m_UserSpot, Vector2.right, Vector2.up, Vector2.one * (Walkway * 0.5f + 0.25f)) };
            return FurnitureItem.Overlap(footprints, user);
        }

        /// <summary>Not through a wall or real furniture (for pieces that don't block the floor, such as rugs).</summary>
        bool ClearOfRoom(Footprint[] footprints)
        {
            if (sceneObstacles == null)
                return true;
            foreach (var wall in sceneObstacles.Walls)
                if (FurnitureItem.Overlap(footprints, new[] { wall }))
                    return false;
            // A rug lies flat, but it isn't laid under a real bed or among a real table's legs.
            return WhatRealBlocks(footprints, 0.5f) == null;
        }

        static Footprint[] Grow(Footprint[] footprints, float by)
        {
            var grown = new Footprint[footprints.Length];
            for (int i = 0; i < footprints.Length; i++)
            {
                var f = footprints[i];
                grown[i] = new Footprint(f.centre, f.axisX, f.axisZ, f.half + Vector2.one * by);
            }
            return grown;
        }

        /// <summary>Slides a piece along the wall from <paramref name="preferredX"/> to the nearest spot where it fits.</summary>
        bool PutAlongWall(FurnitureItem item, Frame f, float preferredX, float minX, float maxX, float backGap, float floorY, float clearance)
        {
            for (float d = 0f; d <= f.halfLength * 2f; d += 0.1f)
            {
                foreach (float sign in new[] { 1f, -1f })
                {
                    float x = preferredX + sign * d;
                    if (x < minX || x > maxX)
                        continue;
                    if (Put(item, f, x, backGap, floorY, 0f, clearance))
                        return true;
                    if (d == 0f)
                        break;
                }
            }
            return false;
        }

        List<FurnitureItem> TryLayOut(Transform root, Frame f, Transform head, float floorY)
        {
            var placed = new List<FurnitureItem>();
            // Where along the wall the user is: the room is centred on them when the wall allows.
            float userX = Vector3.Dot(head.position - f.origin, f.along);
            float wallMin = -f.halfLength, wallMax = f.halfLength;

            // 1–3. The core group — sofa against the wall, coffee table 40 cm in front, rug under both —
            //      slides along the wall (from in front of the user outward) to the first spot where it
            //      all fits; larger sizes are tried first. A spot without room for the rug is a fallback.
            if (!PlaceCoreGroup(root, f, userX, floorY, out var sofa, out var table, out var rug))
                return null;
            placed.Add(sofa);
            placed.Add(table);
            if (rug != null)
                placed.Add(rug);
            var sofaBox = LocalBox(sofa);
            var tableBox = LocalBox(table);
            float sofaX = Vector3.Dot(sofa.transform.position - f.origin, f.along);
            float sofaFront = SofaToWall + sofaBox.size.z;

            // 4. Side table at one arm, floor lamp at the other.
            float sofaRight = sofaX + sofaBox.max.x, sofaLeft = sofaX + sofaBox.min.x;
            var side = Make(root, FurnitureKind.SideTable, "standard");
            float sideHalf = LocalBox(side).extents.x;
            bool sideRight = Put(side, f, sofaRight + SideTableToSofa + sideHalf, SofaToWall + 0.05f, floorY);
            if (!sideRight && !Put(side, f, sofaLeft - SideTableToSofa - sideHalf, SofaToWall + 0.05f, floorY))
                Discard(side);
            else
                placed.Add(side);

            var lamp = Make(root, FurnitureKind.FloorLamp, "tripod");
            float lampHalf = LocalBox(lamp).extents.x;
            float lampX = sideRight ? sofaLeft - 0.05f - lampHalf : sofaRight + 0.05f + lampHalf;
            if (Put(lamp, f, lampX, SofaToWall, floorY))
                placed.Add(lamp);
            else
                Discard(lamp);

            // 5. Armchair at the lamp end of the coffee table, turned in toward the sofa.
            var chair = Make(root, FurnitureKind.Armchair, "standard");
            var chairBox = LocalBox(chair);
            float chairSide = sideRight ? -1f : 1f;
            float chairX = sofaX + chairSide * (tableBox.extents.x + 0.45f + chairBox.extents.z);
            float chairZ = sofaFront + SofaToCoffeeTable + tableBox.size.z * 0.5f + 0.15f;
            // Facing across the table and a little toward the sofa, so it joins the conversation.
            var chairPos = f.Point(chairX, chairZ, floorY);
            var tablePos = table.transform.position;
            var look = Vector3.Lerp(tablePos, sofa.transform.position, 0.35f) - chairPos;
            look.y = 0f;
            var chairRot = Quaternion.LookRotation(look.normalized, Vector3.up);
            if (Fits(chair, chairPos, chairRot, 0f, f))
            {
                chair.transform.SetPositionAndRotation(chairPos, chairRot);
                placed.Add(chair);
            }
            else
            {
                Discard(chair);
            }

            // 6. Bookshelf and plant share out the rest of the wall: each goes halfway into the free
            //    stretch on its side of the pieces standing against the wall. If this wall is full, they
            //    go on another wall, keeping a walkway from everything else.
            float groupMin = float.MaxValue, groupMax = float.MinValue;
            foreach (var item in placed)
            {
                float depth = Vector3.Dot(item.transform.position - f.origin, f.inward);
                if (depth > 1f || item.Kind == FurnitureKind.Rug)
                    continue; // out in the room (armchair, table), not taking up wall
                var b = LocalBox(item);
                float c = Vector3.Dot(item.transform.position - f.origin, f.along);
                groupMin = Mathf.Min(groupMin, c - b.extents.x);
                groupMax = Mathf.Max(groupMax, c + b.extents.x);
            }
            bool shelfRight = sideRight; // books by the side table, the plant at the other end
            FurnitureItem shelf = null;
            Frame shelfFrame = f;
            foreach (var size in new[] { "2x3", "2x2" })
            {
                shelf = Make(root, FurnitureKind.Bookshelf, size);
                if (PlaceInStretch(shelf, f, shelfRight ? groupMax : groupMin, shelfRight ? wallMax : wallMin, floorY, 0.03f)
                    || PlaceInStretch(shelf, f, shelfRight ? groupMin : groupMax, shelfRight ? wallMin : wallMax, floorY, 0.03f))
                    break;
                if (PlaceOnOtherWall(shelf, f, head, floorY, 0.03f, out shelfFrame))
                    break;
                Discard(shelf);
                shelf = null;
            }
            if (shelf != null)
                placed.Add(shelf);

            var plant = Make(root, FurnitureKind.Plant, "large");
            if (PlaceInStretch(plant, f, shelfRight ? groupMin : groupMax, shelfRight ? wallMin : wallMax, floorY, 0.1f)
                || PlaceInStretch(plant, f, shelfRight ? groupMax : groupMin, shelfRight ? wallMax : wallMin, floorY, 0.1f)
                || PlaceOnOtherWall(plant, f, head, floorY, 0.1f, out _))
                placed.Add(plant);
            else
                Discard(plant);

            // 7. Pillows on the sofa; listed together so they alternate accent and highlight.
            PlacePillowsOnSofa(root, sofa, placed);

            // 8. Art on the wall: a large print above the sofa, a smaller one above the bookshelf.
            HangArt(root, f, "a1", sofaX, sofaBox.max.y, floorY, placed);
            if (shelf != null)
            {
                var shelfBox = LocalBox(shelf);
                HangArt(root, shelfFrame, "a2", Vector3.Dot(shelf.transform.position - shelfFrame.origin, shelfFrame.along), shelfBox.max.y, floorY, placed);
            }
            return placed;
        }

        /// <summary>
        /// One pillow in each corner of the sofa: standing on the seat cushion, tucked against the arm,
        /// and leaning back flush with the back cushions at their angle.
        /// </summary>
        void PlacePillowsOnSofa(Transform root, FurnitureItem sofa, List<FurnitureItem> placed)
        {
            float width = LocalBox(sofa).size.x;
            float innerHalf = width / 2f - FurnitureFactory.SofaArmWidth;
            var lean = Quaternion.Euler(-FurnitureFactory.SofaBackLean, 0f, 0f);
            foreach (float side in new[] { -1f, 1f })
            {
                var pillow = Make(root, FurnitureKind.ThrowPillow, "square");
                var box = LocalBox(pillow);
                // Into the corner, a hand's width from the arm so it reads as placed rather than jammed.
                float x = side * (innerHalf - box.extents.x - 0.04f);
                // Its back face rests on the back cushions; the lean pivots about its bottom edge.
                float z = FurnitureFactory.SofaBackCushionFront - box.min.z;
                var local = new Vector3(x, FurnitureFactory.SofaSeatTop, z);
                var rotation = sofa.transform.rotation * Quaternion.Euler(0f, -side * 8f, 0f) * lean; // turned in a touch
                pillow.transform.SetPositionAndRotation(sofa.transform.position + sofa.transform.rotation * local, rotation);
                placed.Add(pillow);
            }
        }

        bool PlaceCoreGroup(Transform root, Frame f, float userX, float floorY, out FurnitureItem sofa, out FurnitureItem table, out FurnitureItem rug)
        {
            sofa = table = rug = null;
            foreach (var sofaSize in new[] { "three", "loveseat" })
            foreach (var tableSize in new[] { "medium", "small" })
            {
                var s = Make(root, FurnitureKind.Sofa, sofaSize);
                var t = Make(root, FurnitureKind.CoffeeTable, tableSize);
                var sofaBox = LocalBox(s);
                float half = sofaBox.extents.x;
                float sofaFront = SofaToWall + sofaBox.size.z;
                float lo = -f.halfLength + half, hi = f.halfLength - half;
                float fallbackX = float.NaN;
                for (float d = 0f; d <= f.halfLength * 2f; d += 0.1f)
                {
                    foreach (float sign in new[] { 1f, -1f })
                    {
                        float x = Mathf.Clamp(userX, lo, hi) + sign * d;
                        if (x < lo || x > hi)
                            continue;
                        if (Put(s, f, x, SofaToWall, floorY) && Put(t, f, x, sofaFront + SofaToCoffeeTable, floorY))
                        {
                            var r = PlaceRug(root, f, x, sofaFront, floorY);
                            if (r != null)
                            {
                                sofa = s; table = t; rug = r;
                                return true;
                            }
                            if (float.IsNaN(fallbackX))
                                fallbackX = x;
                        }
                        if (d == 0f)
                            break;
                    }
                }
                if (!float.IsNaN(fallbackX) && Put(s, f, fallbackX, SofaToWall, floorY) && Put(t, f, fallbackX, sofaFront + SofaToCoffeeTable, floorY))
                {
                    sofa = s; table = t;
                    return true;
                }
                Discard(s);
                Discard(t);
            }
            return false;
        }

        FurnitureItem PlaceRug(Transform root, Frame f, float x, float sofaFront, float floorY)
        {
            foreach (var size in new[] { "5x8", "4x6" })
            {
                var rug = Make(root, FurnitureKind.Rug, size);
                if (Put(rug, f, x, sofaFront - RugUnderSofa, floorY))
                    return rug;
                Discard(rug);
            }
            return null;
        }

        /// <summary>Puts a wall piece (bookshelf, plant) against another solid wall, centred in its free length.</summary>
        bool PlaceOnOtherWall(FurnitureItem item, Frame anchor, Transform head, float floorY, float backGap, out Frame used)
        {
            used = anchor;
            float half = LocalBox(item).extents.x;
            foreach (var g in CandidateFrames(head, floorY))
            {
                if (!g.real)
                    break;
                if (Vector3.Distance(g.origin, anchor.origin) < 0.05f && Vector3.Dot(g.along, anchor.along) > 0.99f)
                    continue;
                if (PutAlongWall(item, g, 0f, -g.halfLength + half, g.halfLength - half, backGap, floorY, Walkway))
                {
                    used = g;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Puts a wall piece in the free stretch between the seating group's edge and the wall's end,
        /// halfway along it so the spacing reads as even, but never closer than a walkway to the group.
        /// </summary>
        bool PlaceInStretch(FurnitureItem item, Frame f, float groupEdge, float wallEnd, float floorY, float backGap)
        {
            const float breathingRoom = 0.45f; // between pieces along the same wall
            float dir = Mathf.Sign(wallEnd - groupEdge);
            float half = LocalBox(item).extents.x;
            float nearest = groupEdge + dir * (breathingRoom + half);
            float farthest = wallEnd - dir * half;
            if ((farthest - nearest) * dir < 0f)
                return false;
            float preferred = (nearest + farthest) * 0.5f;
            float lo = Mathf.Min(nearest, farthest), hi = Mathf.Max(nearest, farthest);
            return PutAlongWall(item, f, preferred, lo, hi, backGap, floorY, 0.3f);
        }

        /// <summary>Hangs a print on the wall: its bottom 22 cm above the furniture below, or centred at 1.45 m.</summary>
        void HangArt(Transform root, Frame f, string size, float x, float furnitureTop, float floorY, List<FurnitureItem> placed)
        {
            var art = Make(root, FurnitureKind.WallArt, size);
            var box = LocalBox(art);
            float bottom = furnitureTop > 0f ? furnitureTop + ArtAboveFurniture : ArtCentreHeight - box.size.y / 2f;
            bottom = Mathf.Max(bottom, ArtCentreHeight - box.size.y / 2f - 0.15f);
            if (f.real && bottom + box.size.y > f.wallTop - 0.1f)
                bottom = Mathf.Max(furnitureTop + 0.1f, f.wallTop - 0.1f - box.size.y);
            var rotation = f.Facing();
            // Centred over the furniture when that stretch of wall is clear; otherwise the nearest clear
            // spot along the wall (a window, door or real picture may already be there).
            for (float d = 0f; d <= 1.2f; d += 0.1f)
            {
                foreach (float sign in new[] { 1f, -1f })
                {
                    var position = f.Point(x + sign * d, 0.005f - box.min.z, floorY) + Vector3.up * bottom;
                    if (f.real && Mathf.Abs(x + sign * d) > f.halfLength - box.extents.x)
                        continue;
                    if (OverOpening(position, box.size.x) || ArtOverlapsArt(art, position, box.size.x))
                        continue;
                    art.transform.SetPositionAndRotation(position, rotation);
                    placed.Add(art);
                    return;
                }
            }
            Discard(art);
        }

        /// <summary>
        /// Hangs a print added from the shopping list on the solid wall the user is facing, at gallery
        /// height (centre 1.45 m) and in a clear stretch of wall. Returns false if there's no wall.
        /// </summary>
        bool TryHangOnWall(FurnitureItem art, Transform head, float floorY)
        {
            var box = LocalBox(art);
            foreach (var f in CandidateFrames(head, floorY))
            {
                if (!f.real)
                    break;
                float userX = Vector3.Dot(head.position - f.origin, f.along);
                float half = box.extents.x;
                for (float d = 0f; d <= f.halfLength; d += 0.15f)
                {
                    foreach (float sign in new[] { 1f, -1f })
                    {
                        float x = Mathf.Clamp(userX + sign * d, -f.halfLength + half, f.halfLength - half);
                        float bottom = ArtCentreHeight - box.size.y / 2f;
                        var position = f.Point(x, 0.005f - box.min.z, floorY) + Vector3.up * bottom;
                        if (OverOpening(position, box.size.x) || ArtOverlapsArt(art, position, box.size.x))
                            continue;
                        art.transform.SetPositionAndRotation(position, f.Facing());
                        return true;
                    }
                }
            }
            return false;
        }

        static readonly FurnitureKind[] Tabletops =
        {
            FurnitureKind.SideTable, FurnitureKind.Nightstand, FurnitureKind.ConsoleTable, FurnitureKind.Dresser,
            FurnitureKind.Desk, FurnitureKind.CoffeeTable, FurnitureKind.MediaConsole, FurnitureKind.DiningTable,
        };

        static readonly FurnitureKind[] Seats =
        {
            FurnitureKind.Sofa, FurnitureKind.Armchair, FurnitureKind.Bench, FurnitureKind.Ottoman,
        };

        /// <summary>
        /// Sets an accessory where it belongs: a lamp or vase on the nearest tabletop, a throw over the
        /// nearest seat. It takes a clear spot on the host's top, away from what's already there.
        /// </summary>
        bool TryPlaceAccessory(FurnitureItem accessory, Transform head)
        {
            var hosts = accessory.Kind == FurnitureKind.ThrowBlanket ? Seats : Tabletops;
            var candidates = new List<FurnitureItem>();
            foreach (var item in m_Items)
                if (item != null && item != accessory && System.Array.IndexOf(hosts, item.Kind) >= 0)
                    candidates.Add(item);
            candidates.Sort((a, b) => Vector3.Distance(head.position, a.transform.position).CompareTo(Vector3.Distance(head.position, b.transform.position)));

            foreach (var host in candidates)
            {
                var box = LocalBox(host);
                float topY = SurfaceHeight(host, box);
                // Spots across the top: the outer thirds first (styled pieces sit off-centre), then the middle.
                foreach (float across in new[] { 0.28f, -0.28f, 0f })
                {
                    var local = new Vector3(box.center.x + across * box.size.x, topY, box.center.z);
                    var position = host.transform.position + host.transform.rotation * local;
                    if (Crowded(position, accessory))
                        continue;
                    accessory.transform.SetPositionAndRotation(position, host.transform.rotation);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Where things rest on a host: the seat for seating (not the top of its back), the top for tables.</summary>
        static float SurfaceHeight(FurnitureItem host, Bounds box)
        {
            switch (host.Kind)
            {
                case FurnitureKind.Sofa: return FurnitureFactory.SofaSeatTop;
                case FurnitureKind.Armchair: return 0.42f; // the scanned lounge chair's cushion
                default: return box.max.y;
            }
        }

        /// <summary>Another accessory already stands here (compared across the floor, so pieces still dropping in count).</summary>
        bool Crowded(Vector3 position, FurnitureItem except)
        {
            var flat = new Vector2(position.x, position.z);
            foreach (var item in m_Items)
                if (item != null && item != except && item.Mount == FurnitureMount.Free &&
                    Vector2.Distance(new Vector2(item.transform.position.x, item.transform.position.z), flat) < 0.22f)
                    return true;
            return false;
        }

        bool ArtOverlapsArt(FurnitureItem art, Vector3 position, float width)
        {
            foreach (var other in m_Items)
            {
                if (other == null || other == art || other.Kind != FurnitureKind.WallArt)
                    continue;
                if (Vector3.Distance(other.transform.position, position) < (width + LocalBox(other).size.x) / 2f + 0.15f)
                    return true;
            }
            return false;
        }

        bool OverOpening(Vector3 position, float width)
        {
            if (sceneObstacles == null)
                return false;
            var p = new Vector2(position.x, position.z);
            foreach (var o in sceneObstacles.Openings)
            {
                var c = new Vector2(o.centre.x, o.centre.z);
                float reach = Mathf.Max(o.size.x, o.size.y) / 2f + width / 2f + 0.1f; // leave a gap between frames
                if (Vector2.Distance(p, c) < reach && Mathf.Abs(position.y - o.centre.y) < Mathf.Max(o.size.x, o.size.y))
                    return true;
            }
            return false;
        }
    }
}
