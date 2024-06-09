using HarmonyLib;
using System;
using System.Collections.Generic;
using TMPro;
using Unfoundry;
using UnityEngine;
using UnityEngine.UI;

namespace Duplicationer
{
    internal class BlueprintFrame : BaseFrame, IEscapeCloseable
    {
        private TextMeshProUGUI _textMaterialReport = null;
        private TextMeshProUGUI _textPositionX = null;
        private TextMeshProUGUI _textPositionY = null;
        private TextMeshProUGUI _textPositionZ = null;
        private GameObject _rowCheats;
        private Button _buttonCheatMode;

        private float _nextUpdateTimeCountTexts = 0.0f;
        private int _materialReportMarkedLine = -1;
        private List<ulong> _materialReportTemplateIds = new List<ulong>();

        private string CheatModeButtonText => DuplicationerPlugin.IsCheatModeEnabled ? "Disable Cheat Mode" : "Enable Cheat Mode";

        public BlueprintFrame(BlueprintToolCHM tool) : base(tool) { }

        public void Show()
        {
            if (IsOpen) return;

            _tool.HideSaveFrame(true);
            _tool.HideLibraryFrame(true);
            _tool.HideFolderFrame(true);

            if (_frameRoot == null)
            {
                var graphics = Traverse.Create(typeof(UIRaycastTooltipManager))?.Field("singleton")?.GetValue<UIRaycastTooltipManager>()?.tooltipRectTransform?.GetComponentsInChildren<Graphic>();
                if (graphics != null)
                {
                    foreach (var graphic in graphics)
                    {
                        graphic.raycastTarget = false;
                    }
                }

                ulong usernameHash = GameRoot.getClientCharacter().usernameHash;
                UIBuilder.BeginWith(GameRoot.getDefaultCanvas())
                    .Element_PanelAutoSize("DuplicationerFrame", "corner_cut_outline", new Color(0.133f, 0.133f, 0.133f, 1.0f), new Vector4(13, 10, 8, 13))
                        .Keep(out _frameRoot)
                        .SetVerticalLayout(new RectOffset(0, 0, 0, 0), 0.0f, TextAnchor.UpperLeft, false, true, true, true, false, false, false)
                        .SetRectTransform(-420.0f, 120.0f, -60.0f, 220.0f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f)
                        .Element_Header("HeaderBar", "corner_cut_outline", new Color(0.0f, 0.6f, 1.0f, 1.0f), new Vector4(13, 3, 8, 13))
                            .SetRectTransform(0.0f, -60.0f, 599.0f, 0.0f, 0.5f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f)
                            .Layout()
                                .MinWidth(340)
                                .MinHeight(60)
                            .Done
                            .Element("Heading")
                                .SetRectTransform(0.0f, 0.0f, -60.0f, 0.0f, 0.0f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                                .Component_Text("Duplicationer", "OpenSansSemibold SDF", 34.0f, Color.white)
                            .Done
                            .Element_Button("Button Close", "corner_cut_fully_inset", Color.white, new Vector4(13.0f, 1.0f, 4.0f, 13.0f))
                                .SetOnClick(() => Hide())
                                .SetRectTransform(-60.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.5f, 1.0f, 0.0f, 1.0f, 1.0f)
                                .SetTransitionColors(new Color(1.0f, 1.0f, 1.0f, 1.0f), new Color(1.0f, 0.25f, 0.0f, 1.0f), new Color(1.0f, 0.0f, 0.0f, 1.0f), new Color(1.0f, 0.25f, 0.0f, 1.0f), new Color(0.5f, 0.5f, 0.5f, 1.0f), 1.0f, 0.1f)
                                .Element("Image")
                                    .SetRectTransform(5.0f, 5.0f, -5.0f, -5.0f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                                    .Component_Image("cross", Color.white, Image.Type.Sliced, Vector4.zero)
                                .Done
                            .Done
                        .Done
                        .Element("Content")
                            .SetRectTransform(0.0f, -855.0f, 599.0f, -60.0f, 0.5f, 0.0f, 0.0f, 1.0f, 0.0f, 1.0f)
                            .SetVerticalLayout(new RectOffset(10, 10, 10, 10), 0.0f, TextAnchor.UpperLeft, false, true, true, true, false, false, false)
                            .Element("Padding")
                                .SetRectTransform(10.0f, -785.0f, 589.0f, -10.0f, 0.5f, 0.5f, 0.0f, 1.0f, 0.0f, 1.0f)
                                .SetVerticalLayout(new RectOffset(0, 0, 0, 0), 10.0f, TextAnchor.UpperLeft, false, true, true, true, false, false, false)
                                .Element_ScrollBox("Material Report ScrollBox", builder =>
                                {
                                    builder = builder
                                        .SetVerticalLayout(new RectOffset(5, 5, 5, 5), 10.0f, TextAnchor.UpperLeft, false, true, true, false, false, false, false)
                                        .Element("Material Report")
                                            .SetRectTransform(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f)
                                            .AutoSize(ContentSizeFitter.FitMode.PreferredSize, ContentSizeFitter.FitMode.PreferredSize)
                                            .Component_Text("", "OpenSansSemibold SDF", 14.0f, Color.white, TextAlignmentOptions.TopLeft)
                                            .Keep(out _textMaterialReport)
                                        .Done;
                                    var clickHandler = _textMaterialReport.gameObject.AddComponent<MaterialReportClickHandler>();
                                    clickHandler.OnLineChanged += MaterialReportMarkedLineChanged;
                                    clickHandler.OnLineClicked += MaterialReportMarkedLineClicked;
                                })
                                    .Layout()
                                        .PreferredHeight(200)
                                    .Done
                                .Done
                                .Element("Row Clear")
                                    .Updater(_guiUpdaters, (GameObject row) => row.SetActive(_tool.CurrentBlueprint != null && _tool.CurrentBlueprint.HasRecipes))
                                    .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                    .Element_TextButton("Clear Recipes Button", "Clear Recipes", Color.white)
                                        .SetOnClick(_tool.ClearBlueprintRecipes)
                                    .Done
                                .Done
                                .Element("Demolish Row")
                                    .Updater(_guiUpdaters, () => _tool.boxMode != BlueprintToolCHM.BoxMode.None && _tool.CurrentMode != _tool.modeSelectArea)
                                    .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                    .Element_Label("Demolish Label", "Demolish ", 100, 1)
                                    .Done
                                    .Element_ImageButton("Button Demolish Buildings", "assembler_iii")
                                        .Component_Tooltip("Demolish\nBuildings")
                                        .SetOnClick(() => _tool.DemolishArea(true, false, false, false))
                                    .Done
                                    .Element_ImageButton("Button Demolish Blocks", "floor")
                                        .Component_Tooltip("Demolish\nBlocks")
                                        .SetOnClick(() => _tool.DemolishArea(false, true, false, false))
                                    .Done
                                    .Element_ImageButton("Button Demolish Terrain", "dirt")
                                        .Component_Tooltip("Demolish\nTerrain")
                                        .SetOnClick(() => _tool.DemolishArea(false, false, true, false))
                                    .Done
                                    .Element_ImageButton("Button Demolish Decor", "biomass")
                                        .Component_Tooltip("Demolish\nDecor")
                                        .SetOnClick(() => _tool.DemolishArea(false, false, false, true))
                                    .Done
                                    .Element_ImageButton("Button Demolish All", "icons8-error-100")
                                        .Component_Tooltip("Demolish\nAll")
                                        .SetOnClick(() => _tool.DemolishArea(true, true, true, true))
                                    .Done
                                .Done
                                .Element("Destroy Row")
                                    .Updater(_guiUpdaters, () => _tool.boxMode != BlueprintToolCHM.BoxMode.None && _tool.CurrentMode != _tool.modeSelectArea)
                                    .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                    .Element_Label("Destroy Label", "Destroy ", 100, 1)
                                    .Done
                                    //.Element_ImageButton("Button Destroy Buildings", "assembler_iii")
                                    //    .Component_Tooltip("Destroy\nBuildings")
                                    //    .SetOnClick(() => ConfirmationFrame.Show("Permanently destroy buildings in selection?", () => DestroyArea(true, false, false, false)))
                                    //.Done
                                    //.Element_ImageButton("Button Destroy Blocks", "floor")
                                    //    .Component_Tooltip("Destroy\nBlocks")
                                    //    .SetOnClick(() => ConfirmationFrame.Show("Permanently destroy foundation blocks in selection?", () => DestroyArea(false, true, false, false)))
                                    //.Done
                                    .Element_ImageButton("Button Destroy Terrain", "dirt")
                                        .Component_Tooltip("Destroy\nTerrain")
                                        .SetOnClick(() => ConfirmationFrame.Show("Permanently destroy terrain in selection?", () => _tool.DestroyArea(false, false, true, false)))
                                    .Done
                                    .Element_ImageButton("Button Destroy Decor", "biomass")
                                        .Component_Tooltip("Destroy\nDecor")
                                        .SetOnClick(() => ConfirmationFrame.Show("Permanently destroy plants in selection?", () => _tool.DestroyArea(false, false, false, true)))
                                    .Done
                                    .Element_ImageButton("Button Destroy All", "icons8-error-100")
                                        .Component_Tooltip("Destroy\nAll")
                                        .SetOnClick(() => ConfirmationFrame.Show("Permanently destroy everything in selection?", () => _tool.DestroyArea(true, true, true, true)))
                                    .Done
                                .Done
                                .Element("Position Row")
                                    .Updater(_guiUpdaters, () => _tool.boxMode == BlueprintToolCHM.BoxMode.Blueprint)
                                    .AutoSize(ContentSizeFitter.FitMode.Unconstrained, ContentSizeFitter.FitMode.PreferredSize)
                                    .SetVerticalLayout(new RectOffset(0, 0, 0, 0), 10.0f, TextAnchor.UpperLeft, false, true, true, true, false, false, false)
                                    .Element("Row Position X")
                                        .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                        .Element("Position Display X")
                                            .Layout()
                                                .MinWidth(100)
                                                .FlexibleWidth(1)
                                            .Done
                                            .Component_Text("X: 0", "OpenSansSemibold SDF", 18.0f, Color.white, TextAlignmentOptions.MidlineLeft)
                                            .Keep(out _textPositionX)
                                        .Done
                                        .Element_ImageButton("Button Decrease", "icons8-chevron-left-filled-100_white", 28, 28, 90.0f)
                                            .SetOnClick(() => { _tool.MoveBlueprint(_tool.CurrentBlueprintAnchor + new Vector3Int(-1, 0, 0) * _tool.NudgeX); })
                                        .Done
                                        .Element_ImageButton("Button Increase", "icons8-chevron-left-filled-100_white", 28, 28, 270.0f)
                                            .SetOnClick(() => { _tool.MoveBlueprint(_tool.CurrentBlueprintAnchor + new Vector3Int(1, 0, 0) * _tool.NudgeX); })
                                        .Done
                                    .Done
                                    .Element("Row Position Y")
                                        .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                        .Element("Position Display Y")
                                            .Layout()
                                                .MinWidth(100)
                                                .FlexibleWidth(1)
                                            .Done
                                            .Component_Text("Y: 0", "OpenSansSemibold SDF", 18.0f, Color.white, TextAlignmentOptions.MidlineLeft)
                                            .Keep(out _textPositionY)
                                        .Done
                                        .Element_ImageButton("Button Decrease", "icons8-chevron-left-filled-100_white", 28, 28, 90.0f)
                                            .SetOnClick(() => { _tool.MoveBlueprint(_tool.CurrentBlueprintAnchor + new Vector3Int(0, -1, 0) * _tool.NudgeY); })
                                        .Done
                                        .Element_ImageButton("Button Increase", "icons8-chevron-left-filled-100_white", 28, 28, 270.0f)
                                            .SetOnClick(() => { _tool.MoveBlueprint(_tool.CurrentBlueprintAnchor + new Vector3Int(0, 1, 0) * _tool.NudgeY); })
                                        .Done
                                    .Done
                                    .Element("Row Position Z")
                                        .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                        .Element("Position Display Z")
                                            .Layout()
                                                .MinWidth(100)
                                                .FlexibleWidth(1)
                                            .Done
                                            .Component_Text("Z: 0", "OpenSansSemibold SDF", 18.0f, Color.white, TextAlignmentOptions.MidlineLeft)
                                            .Keep(out _textPositionZ)
                                        .Done
                                        .Element_ImageButton("Button Decrease", "icons8-chevron-left-filled-100_white", 28, 28, 90.0f)
                                            .SetOnClick(() => { _tool.MoveBlueprint(_tool.CurrentBlueprintAnchor + new Vector3Int(0, 0, -1) * _tool.NudgeZ); })
                                        .Done
                                        .Element_ImageButton("Button Increase", "icons8-chevron-left-filled-100_white", 28, 28, 270.0f)
                                            .SetOnClick(() => { _tool.MoveBlueprint(_tool.CurrentBlueprintAnchor + new Vector3Int(0, 0, 1) * _tool.NudgeZ); })
                                        .Done
                                    .Done
                                .Done
                                .Element("Preview Opacity Row")
                                    .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                    .Element_Label("Preview Opacity Label", "Preview Opacity", 150, 1)
                                    .Done
                                    .Element_Slider("Preview Opacity Slider", DuplicationerPlugin.configPreviewAlpha.Get(), 0.0f, 1.0f, (value) => { DuplicationerPlugin.configPreviewAlpha.Set(value); _tool.SetPlaceholderOpacity(value); })
                                        .Layout()
                                            .MinWidth(200)
                                            .MinHeight(40)
                                            .FlexibleWidth(1)
                                        .Done
                                    .Done
                                .Done
                                .Element("Row Cheats")
                                    .Keep(out _rowCheats)
                                    .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                    .Element_TextButton("Button Cheat Mode", CheatModeButtonText, Color.white)
                                        .Keep(out _buttonCheatMode)
                                        .Component_Tooltip("Toggle cheat mode")
                                        .SetOnClick(ToggleCheatMode)
                                    .Done
                                .Done
                                .Element("Row Files")
                                    .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                    .Element_ImageTextButton("Button Save", "Save", "download", Color.white, 28, 28)
                                        .Component_Tooltip("Save current blueprint")
                                        .SetOnClick(_tool.BeginSaveBlueprint)
                                        .Updater<Button>(_guiUpdaters, () => _tool.IsBlueprintLoaded)
                                    .Done
                                    .Element_ImageTextButton("Button Load", "Load", "upload", Color.white, 28, 28)
                                        .Component_Tooltip("Load blueprint from library")
                                        .SetOnClick(_tool.BeginLoadBlueprint)
                                    .Done
                                .Done
                                .Element("Row Confirm Buttons")
                                    .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                    .Element_TextButton("Button Paste", "Confirm/Paste")
                                        .Updater<Button>(_guiUpdaters, () => _tool.CurrentMode != null && _tool.CurrentMode.AllowPaste(_tool))
                                        .SetOnClick(() => { _tool.PlaceBlueprintMultiple(_tool.CurrentBlueprintAnchor, _tool.repeatFrom, _tool.repeatTo); })
                                    .Done
                                    .Element_TextButton("Button Copy", "Confirm/Copy")
                                        .Updater<Button>(_guiUpdaters, () => _tool.CurrentMode != null && _tool.CurrentMode.AllowCopy(_tool))
                                        .SetOnClick(_tool.CopySelection)
                                    .Done
                                .Done
                            .Done
                        .Done
                    .Done
                .End();

                if (!DuplicationerPlugin.configCheatModeAllowed.Get() && _rowCheats != null)
                {
                    _rowCheats.SetActive(false);
                }
            }

            Shown();

            UpdateBlueprintPositionText();
        }

        private void MaterialReportMarkedLineChanged(int line)
        {
            _materialReportMarkedLine = line;
        }

        private void MaterialReportMarkedLineClicked(int line)
        {
            if (line < 0 || line >= _materialReportTemplateIds.Count) return;

            var templateId = _materialReportTemplateIds[line];
            var template = ItemTemplateManager.getItemTemplate(templateId);
            if (template == null) return;

            ConfirmationFrame.Show($"Remove all '{template.name}'?", "Remove", () => {
                _tool.RemoveItemFromBlueprint(template);
            });

            ForceUpdateMaterialReport();
        }

        internal void ToggleCheatMode()
        {
            if (_buttonCheatMode == null) return;
            if (!DuplicationerPlugin.configCheatModeAllowed.Get()) return;
            DuplicationerPlugin.configCheatModeEnabled.Set(!DuplicationerPlugin.configCheatModeEnabled.Get());
            var textComponent = _buttonCheatMode.GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent == null) return;
            textComponent.text = CheatModeButtonText;
        }

        internal void UpdateBlueprintPositionText()
        {
            if (!IsOpen) return;

            if (_textPositionX != null) _textPositionX.text = string.Format("Position X: {0}", _tool.CurrentBlueprintAnchor.x);
            if (_textPositionY != null) _textPositionY.text = string.Format("Position Y: {0}", _tool.CurrentBlueprintAnchor.y);
            if (_textPositionZ != null) _textPositionZ.text = string.Format("Position Z: {0}", _tool.CurrentBlueprintAnchor.z);
        }

        internal void UpdateMaterialReport()
        {
            if (!IsOpen || _textMaterialReport == null || Time.time < _nextUpdateTimeCountTexts) return;

            ForceUpdateMaterialReport();
        }

        internal void ForceUpdateMaterialReport()
        {
            _nextUpdateTimeCountTexts = Time.time + 0.5f;

            int repeatCount = _tool.RepeatCount.x * _tool.RepeatCount.y * _tool.RepeatCount.z;
            ulong inventoryId = GameRoot.getClientCharacter().inventoryId;
            ulong inventoryPtr = inventoryId != 0 ? InventoryManager.inventoryManager_getInventoryPtr(inventoryId) : 0;

            var materialReportBuilder = new System.Text.StringBuilder();
            var lineIndex = 0;
            void AppendLine(string text)
            {
                if (lineIndex == _materialReportMarkedLine) materialReportBuilder.AppendLine($"<mark>{text}</mark>");
                else materialReportBuilder.AppendLine(text);
                lineIndex++;
            }

            _materialReportTemplateIds.Clear();

            int totalItemCount = 0;
            int totalDoneCount = 0;
            foreach (var kv in _tool.CurrentBlueprint.ShoppingList)
            {
                var itemCount = kv.Value.count * repeatCount;
                if (itemCount > 0)
                {
                    totalItemCount += itemCount;

                    var name = kv.Value.name;
                    var templateId = kv.Value.itemTemplateId;
                    if (templateId != 0)
                    {
                        var doneCount = BlueprintPlaceholder.GetStateCount(templateId, BlueprintPlaceholder.State.Done);
                        totalDoneCount += doneCount;

                        _materialReportTemplateIds.Add(templateId);

                        if (inventoryPtr != 0)
                        {
                            var inventoryCount = InventoryManager.inventoryManager_countByItemTemplateByPtr(inventoryPtr, templateId, IOBool.iotrue);

                            if (doneCount > 0)
                            {
                                AppendLine($"<color=#CCCCCC>{name}:</color> {itemCount - doneCount} <color=#FFFFAA>({inventoryCount})</color> (<color=#AACCFF>{doneCount}</color>/{itemCount})");
                            }
                            else
                            {
                                AppendLine($"<color=#CCCCCC>{name}:</color> {itemCount} <color=#FFFFAA>({inventoryCount})</color>");
                            }
                        }
                        else
                        {
                            AppendLine($"<color=#CCCCCC>{name}:</color> {itemCount} <color=#FFFFAA>(###)</color>");
                        }
                    }
                    else
                    {
                        AppendLine($"<color=#CCCCCC>{name}:</color> {itemCount}");
                    }
                }
            }

            if (totalItemCount > 0)
            {
                if (totalDoneCount > 0)
                {
                    materialReportBuilder.AppendLine($"<color=#CCCCCC>Total:</color> {totalItemCount - totalDoneCount} (<color=#AACCFF>{totalDoneCount}</color>/{totalItemCount})");
                }
                else
                {
                    materialReportBuilder.AppendLine($"<color=#CCCCCC>Total:</color> {totalItemCount}");
                }
            }

            _textMaterialReport.text = materialReportBuilder.ToString();
        }
    }
}
