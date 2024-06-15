using Unfoundry;
using UnityEngine;

namespace Duplicationer
{
    internal class BlueprintToolModePlace : BlueprintToolMode
    {
        public BlueprintToolMode FallbackMode { get; private set; } = null;
        public BlueprintToolMode NextMode { get; private set; } = null;

        public BlueprintToolModePlace()
        {
        }

        public override string TabletTitle(BlueprintToolCHM tool) => "Place Blueprint";

        public override void Enter(BlueprintToolCHM tool, BlueprintToolMode fromMode)
        {
            if (!tool.IsBlueprintLoaded)
            {
                tool.SelectMode(FallbackMode);
                return;
            }

            TabletHelper.SetTabletTextQuickActions($@"{GameRoot.getHotkeyStringFromAction("Action")}: Place Blueprint
{GameRoot.getHotkeyStringFromAction("RotateY")}: Rotate");
            tool.HideBlueprint();
            tool.boxMode = BlueprintToolCHM.BoxMode.None;
        }

        public override bool AllowCopy(BlueprintToolCHM tool) => false;
        public override bool AllowPaste(BlueprintToolCHM tool) => false;
        public override bool AllowRotate(BlueprintToolCHM tool) => true;
        public override bool AllowMirror(BlueprintToolCHM tool) => true;

        public override void Update(BlueprintToolCHM tool)
        {
            if (GlobalStateManager.getRewiredPlayer0().GetButtonDown("Action") && InputHelpers.IsMouseInputAllowed && !tool.IsAnyFrameOpen)
            {
                tool.boxMode = BlueprintToolCHM.BoxMode.None;
                tool.HideBlueprint();
            }
            if (/*GlobalStateManager.getRewiredPlayer0().GetButton("Action") && */InputHelpers.IsMouseInputAllowed && !tool.IsAnyFrameOpen)
            {
                tool.repeatFrom = tool.repeatTo = Vector3Int.zero;
                tool.boxMode = BlueprintToolCHM.BoxMode.Blueprint;

                var character = GameRoot.getClientRenderCharacter();
                if (character != null)
                {
                    if (character.getConstructionRaycastInteractionTarget(300.0f, out var targetCube, out var faceTarget, out var hitInfo, new Vector3Int(1, 1, 1)) >= 0)
                    {
                        var edgeFaceTarget = -1;
                        var edgeBlockPosition = new Vector3Int(0, -1000, 0);
                        if (hitInfo.normal.y >= 0.99)
                        {
                            var hitPointLowered = hitInfo.point + new Vector3(0.0f, -0.5f, 0.0f);
                            var targetBlockLowered = new Vector3Int((int)hitPointLowered.x, (int)hitPointLowered.y, (int)hitPointLowered.z);
                            if (hitPointLowered.x < 0.0) --targetBlockLowered.x;
                            if (hitPointLowered.z < 0.0) --targetBlockLowered.z;
                            var topCenter = (Vector3)targetBlockLowered + new Vector3(0.5f, 1f, 0.5f);
                            var distanceFromTopCenter = (double)Vector3.Distance(topCenter, hitInfo.point);
                            var offsetFromTopCenter = hitInfo.point - topCenter;
                            var angleFromRight = (double)Mathf.Acos(Vector3.Dot(offsetFromTopCenter, Vector3.right) / offsetFromTopCenter.magnitude) * 57.2957801818848;
                            var a2 = 0.3f;
                            var b2 = Mathf.Sqrt((float)((double)a2 * (double)a2 * 2.0));
                            var angleFromRightWrapped90 = (float)(angleFromRight % 90.0);
                            if ((double)angleFromRightWrapped90 > 45.0)
                                angleFromRightWrapped90 = 90f - angleFromRightWrapped90;
                            var minDistanceForEdgeSnap = (double)Mathf.Lerp(a2, b2, angleFromRightWrapped90 / 45f);
                            if (distanceFromTopCenter >= minDistanceForEdgeSnap)
                            {
                                var offset = Vector3Int.zero;
                                offsetFromTopCenter = Quaternion.Euler(0.0f, 45f, 0.0f) * offsetFromTopCenter;
                                if (offsetFromTopCenter.x >= 0.0 && offsetFromTopCenter.z >= 0.0)
                                {
                                    ++offset.z;
                                    edgeFaceTarget = 4;
                                }
                                else if (offsetFromTopCenter.x < 0.0 && offsetFromTopCenter.z < 0.0)
                                {
                                    --offset.z;
                                    edgeFaceTarget = 5;
                                }
                                else if (offsetFromTopCenter.x >= 0.0 && offsetFromTopCenter.z < 0.0)
                                {
                                    ++offset.x;
                                    edgeFaceTarget = 0;
                                }
                                else if (offsetFromTopCenter.x < 0.0 && offsetFromTopCenter.z >= 0.0)
                                {
                                    --offset.x;
                                    edgeFaceTarget = 1;
                                }
                                edgeBlockPosition = targetBlockLowered + offset;
                            }
                        }
                        else if (hitInfo.normal.y <= -0.99)
                        {
                            var hitPointRaised = hitInfo.point + new Vector3(0.0f, 0.5f, 0.0f);
                            var targetBlockRaised = new Vector3Int((int)hitPointRaised.x, (int)hitPointRaised.y, (int)hitPointRaised.z);
                            if (hitPointRaised.x < 0.0) --targetBlockRaised.x;
                            if (hitPointRaised.z < 0.0) --targetBlockRaised.z;
                            var bottomCenter = (Vector3)targetBlockRaised + new Vector3(0.5f, 0.0f, 0.5f);
                            var distanceFromBottomCenter = (double)Vector3.Distance(bottomCenter, hitInfo.point);
                            var offsetFromBottomCenter = hitInfo.point - bottomCenter;
                            var angleFromRight = (double)Mathf.Acos(Vector3.Dot(offsetFromBottomCenter, Vector3.right) / offsetFromBottomCenter.magnitude) * 57.2957801818848;
                            var a2 = 0.3f;
                            var b2 = Mathf.Sqrt((float)((double)a2 * (double)a2 * 2.0));
                            var angleFromRightWrapped90 = (float)(angleFromRight % 90.0);
                            if ((double)angleFromRightWrapped90 > 45.0)
                                angleFromRightWrapped90 = 90f - angleFromRightWrapped90;
                            var minDistanceForEdgeSnap = (double)Mathf.Lerp(a2, b2, angleFromRightWrapped90 / 45f);
                            if (distanceFromBottomCenter >= minDistanceForEdgeSnap)
                            {
                                var offset = Vector3Int.zero;
                                offsetFromBottomCenter = Quaternion.Euler(0.0f, 45f, 0.0f) * offsetFromBottomCenter;
                                if (offsetFromBottomCenter.x >= 0.0 && offsetFromBottomCenter.z >= 0.0)
                                {
                                    ++offset.z;
                                    edgeFaceTarget = 4;
                                }
                                else if (offsetFromBottomCenter.x < 0.0 && offsetFromBottomCenter.z < 0.0)
                                {
                                    --offset.z;
                                    edgeFaceTarget = 5;
                                }
                                else if (offsetFromBottomCenter.x >= 0.0 && offsetFromBottomCenter.z < 0.0)
                                {
                                    ++offset.x;
                                    edgeFaceTarget = 0;
                                }
                                else if (offsetFromBottomCenter.x < 0.0 && offsetFromBottomCenter.z >= 0.0)
                                {
                                    --offset.x;
                                    edgeFaceTarget = 1;
                                }
                                edgeBlockPosition = targetBlockRaised + offset;
                            }
                        }
                        if (edgeBlockPosition.y != -1000 && ChunkManager.chunks_checkBlockEmpty(edgeBlockPosition.x, edgeBlockPosition.y, edgeBlockPosition.z, IOBool.iotrue, IOBool.iotrue) == IOBool.iotrue)
                        {
                            targetCube[0] = edgeBlockPosition.x;
                            targetCube[1] = edgeBlockPosition.y;
                            targetCube[2] = edgeBlockPosition.z;
                            faceTarget = edgeFaceTarget;
                        }
                        else
                        {
                            Vector3 vector3_2 = Vector3.zero;
                            if (hitInfo.normal.y >= 0.99)
                                vector3_2 = hitInfo.point + new Vector3(0.0f, -0.5f, 0.0f);
                            else if (hitInfo.normal.y <= -0.99)
                                vector3_2 = hitInfo.point + new Vector3(0.0f, 0.5f, 0.0f);
                            Vector3Int vector3Int3 = new Vector3Int((int)vector3_2.x, (int)vector3_2.y, (int)vector3_2.z);
                            if (vector3_2.x < 0.0)
                                --vector3Int3.x;
                            if (vector3_2.z < 0.0)
                                --vector3Int3.z;
                            Vector3 vector3_3 = hitInfo.point - ((Vector3)vector3Int3 + new Vector3(0.5f, 0.5f, 0.5f));
                            Vector3 vector3_4 = Quaternion.Euler(0.0f, 45f, 0.0f) * vector3_3;
                            Vector3Int offset = Vector3Int.zero;
                            Vector2 vector2 = (Vector2)new Vector3(hitInfo.point.x - (float)(int)hitInfo.point.x, hitInfo.point.z - (float)(int)hitInfo.point.z);
                            if (vector2.x < 0.18 && vector2.y < 0.18)
                            {
                                if (vector3_4.x < 0.0 && vector3_4.z < 0.0)
                                {
                                    --offset.x;
                                    edgeFaceTarget = 1;
                                }
                                else if (vector3_4.x < 0.0 && vector3_4.z >= 0.0)
                                {
                                    --offset.z;
                                    edgeFaceTarget = 5;
                                }
                            }
                            else if (vector2.x > 0.82 && vector2.y < 0.18)
                            {
                                if (vector3_4.x < 0.0 && vector3_4.z < 0.0)
                                {
                                    ++offset.x;
                                    edgeFaceTarget = 0;
                                }
                                else if (vector3_4.x >= 0.0 && vector3_4.z < 0.0)
                                {
                                    --offset.z;
                                    edgeFaceTarget = 5;
                                }
                            }
                            else if (vector2.x < 0.18 && vector2.y > 0.82)
                            {
                                if (vector3_4.x >= 0.0 && vector3_4.z >= 0.0)
                                {
                                    --offset.x;
                                    edgeFaceTarget = 1;
                                }
                                else if (vector3_4.x < 0.0 && vector3_4.z >= 0.0)
                                {
                                    ++offset.z;
                                    edgeFaceTarget = 4;
                                }
                            }
                            else if (vector2.x > 0.82 && vector2.y > 0.82)
                            {
                                if (vector3_4.x >= 0.0 && vector3_4.z >= 0.0)
                                {
                                    ++offset.x;
                                    edgeFaceTarget = 0;
                                }
                                else if (vector3_4.x >= 0.0 && vector3_4.z < 0.0)
                                {
                                    ++offset.z;
                                    edgeFaceTarget = 4;
                                }
                            }
                            edgeBlockPosition = vector3Int3 + offset;
                            if (edgeBlockPosition.y != -1000 && ChunkManager.chunks_checkBlockEmpty(edgeBlockPosition.x, edgeBlockPosition.y, edgeBlockPosition.z, IOBool.iotrue, IOBool.iotrue) == IOBool.iotrue)
                            {
                                targetCube[0] = edgeBlockPosition.x;
                                targetCube[1] = edgeBlockPosition.y;
                                targetCube[2] = edgeBlockPosition.z;
                                faceTarget = edgeFaceTarget;
                            }
                            else
                            {
                                switch (faceTarget)
                                {
                                    case 0:
                                        targetCube += new Vector3Int(1, 0, 0);
                                        break;

                                    case 1:
                                        targetCube += new Vector3Int(-1, 0, 0);
                                        break;

                                    case 2:
                                        targetCube += new Vector3Int(0, 1, 0);
                                        break;

                                    case 3:
                                        targetCube += new Vector3Int(0, -1, 0);
                                        break;

                                    case 4:
                                        targetCube += new Vector3Int(0, 0, 1);
                                        break;

                                    case 5:
                                        targetCube += new Vector3Int(0, 0, -1);
                                        break;
                                }
                            }
                        }
                    }

                    switch (faceTarget)
                    {
                        case 0:
                            tool.ShowBlueprint(targetCube - new Vector3Int(0, 0, tool.CurrentBlueprint.SizeZ / 2));
                            break;

                        case 1:
                            tool.ShowBlueprint(targetCube - new Vector3Int(tool.CurrentBlueprint.SizeX - 1, 0, tool.CurrentBlueprint.SizeZ / 2));
                            break;

                        case 2:
                            tool.ShowBlueprint(targetCube - new Vector3Int(tool.CurrentBlueprint.SizeX / 2, 0, tool.CurrentBlueprint.SizeZ / 2));
                            break;

                        case 3:
                            tool.ShowBlueprint(targetCube - new Vector3Int(tool.CurrentBlueprint.SizeX / 2, tool.CurrentBlueprint.SizeY - 1, tool.CurrentBlueprint.SizeZ / 2));
                            break;

                        case 4:
                            tool.ShowBlueprint(targetCube - new Vector3Int(tool.CurrentBlueprint.SizeX / 2, 0, 0));
                            break;

                        case 5:
                            tool.ShowBlueprint(targetCube - new Vector3Int(tool.CurrentBlueprint.SizeX / 2, 0, tool.CurrentBlueprint.SizeZ - 1));
                            break;
                    }
                }
            }
            if (GlobalStateManager.getRewiredPlayer0().GetButtonUp("Action") && tool.IsBlueprintActive && InputHelpers.IsMouseInputAllowed && !tool.IsAnyFrameOpen)
            {
                tool.SelectMode(NextMode);
                TabletHelper.SetTabletTextQuickActions("");
            }
        }

        internal void Connect(BlueprintToolMode fallbackMode, BlueprintToolMode nextMode)
        {
            FallbackMode = fallbackMode;
            NextMode = nextMode;
        }
    }
}