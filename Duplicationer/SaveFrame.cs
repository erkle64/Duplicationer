using TMPro;
using Unfoundry;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine;
using System.IO;

namespace Duplicationer
{
    internal class SaveFrame : BaseFrame, IEscapeCloseable
    {
        private GameObject _saveGridObject = null;
        private GameObject _saveFramePreviewContainer = null;
        private Image[] _saveFrameIconImages = new Image[4] { null, null, null, null };
        private Image[] _saveFramePreviewIconImages = new Image[4] { null, null, null, null };
        private TMP_InputField _saveFrameNameInputField = null;
        private TextMeshProUGUI _saveFramePreviewLabel = null;
        private TextMeshProUGUI _saveFrameMaterialReportText = null;
        private ItemTemplate[] _saveFrameIconItemTemplates = new ItemTemplate[4] { null, null, null, null };
        private int _saveFrameIconCount = 0;

        public string BlueprintName
        {
            get => _saveFrameNameInputField?.text ?? string.Empty;
            set {
                if (_saveFrameNameInputField != null) _saveFrameNameInputField.text = value;
            }
        }
        public ItemTemplate[] IconItemTemplates
        {
            get => _saveFrameIconItemTemplates;
            set => _saveFrameIconItemTemplates = value;
        }
        public int IconCount
        {
            get => _saveFrameIconCount;
            set => _saveFrameIconCount = value;
        }

        public SaveFrame(BlueprintToolCHM tool) : base(tool) { }

        public void Show()
        {
            if (IsOpen) return;

            _tool.HideBlueprintFrame(true);
            _tool.HideLibraryFrame(true);
            _tool.HideFolderFrame(true);

            if (_frameRoot == null)
            {
                ulong usernameHash = GameRoot.getClientCharacter().usernameHash;
                UIBuilder.BeginWith(GameRoot.getDefaultCanvas())
                    .Element_Panel("Save Frame", "corner_cut_outline", new Color(0.133f, 0.133f, 0.133f, 1.0f), new Vector4(13, 10, 8, 13))
                        .Keep(out _frameRoot)
                        .SetRectTransform(100, 100, -100, -100, 0.5f, 0.5f, 0, 0, 1, 1)
                        .Element_Header("HeaderBar", "corner_cut_outline", new Color(0.0f, 0.6f, 1.0f, 1.0f), new Vector4(13, 3, 8, 13))
                            .SetRectTransform(0.0f, -60.0f, 0.0f, 0.0f, 0.5f, 1.0f, 0.0f, 1.0f, 1.0f, 1.0f)
                            .Element("Heading")
                                .SetRectTransform(0.0f, 0.0f, -60.0f, 0.0f, 0.0f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                                .Component_Text("Save Blueprint", "OpenSansSemibold SDF", 34.0f, Color.white)
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
                            .SetRectTransform(0.0f, 0.0f, 0.0f, -60.0f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                            .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 0, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                            .Element("ContentLeft")
                                .Layout()
                                    .FlexibleWidth(1)
                                .Done
                                .Element("Padding")
                                    .SetRectTransform(10.0f, 10.0f, -10.0f, -10.0f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                                    .Do(builder =>
                                    {
                                        var gameObject = UnityEngine.Object.Instantiate(_tool.prefabGridScrollView.Prefab, builder.GameObject.transform);
                                        var grid = gameObject.GetComponentInChildren<GridLayoutGroup>();
                                        if (grid == null) throw new System.Exception("Grid not found.");
                                        _saveGridObject = grid.gameObject;
                                        grid.cellSize = new Vector2(80.0f, 80.0f);
                                        grid.padding = new RectOffset(4, 4, 4, 4);
                                        grid.spacing = new Vector2(0.0f, 0.0f);
                                    })
                                .Done
                            .Done
                            .Element("ContentRight")
                                .Layout()
                                    .MinWidth(132 + 4 + 132 + 4 + 132 + 10)
                                    .FlexibleWidth(0)
                                .Done
                                .SetVerticalLayout(new RectOffset(0, 10, 10, 10), 10, TextAnchor.UpperLeft, false, true, true, true, false, false, false)
                                .Element("Icons Row")
                                    .Layout()
                                        .MinHeight(132 + 6 + 132)
                                        .FlexibleHeight(0)
                                    .Done
                                    .Element_Button("Icon 1 Button", _tool.iconBlack.Sprite, Color.white, Vector4.zero, Image.Type.Simple)
                                        .SetRectTransform(0, -132, 132, 0, 0, 1, 0, 1, 0, 1)
                                        .SetOnClick(() => SaveFrameRemoveIcon(0))
                                        .SetTransitionColors(new Color(0.2f, 0.2f, 0.2f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.222f, 0.667f, 1.0f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.5f, 0.5f, 0.5f, 1.0f), 1.0f, 0.1f)
                                        .Element("Image")
                                            .SetRectTransform(0, 0, 0, 0, 0.5f, 0.5f, 0, 0, 1, 1)
                                            .Component_Image(_tool.iconEmpty.Sprite, Color.white, Image.Type.Sliced, Vector4.zero)
                                            .Keep(out _saveFrameIconImages[0])
                                        .Done
                                    .Done
                                    .Element_Button("Icon 2 Button", _tool.iconBlack.Sprite, Color.white, Vector4.zero, Image.Type.Simple)
                                        .SetRectTransform(132 + 4, -132, 132 + 4 + 132, 0, 0, 1, 0, 1, 0, 1)
                                        .SetOnClick(() => SaveFrameRemoveIcon(1))
                                        .SetTransitionColors(new Color(0.2f, 0.2f, 0.2f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.222f, 0.667f, 1.0f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.5f, 0.5f, 0.5f, 1.0f), 1.0f, 0.1f)
                                        .Element("Image")
                                            .SetRectTransform(0, 0, 0, 0, 0.5f, 0.5f, 0, 0, 1, 1)
                                            .Component_Image(_tool.iconEmpty.Sprite, Color.white, Image.Type.Sliced, Vector4.zero)
                                        .Keep(out _saveFrameIconImages[1])
                                        .Done
                                    .Done
                                    .Element_Button("Icon 3 Button", _tool.iconBlack.Sprite, Color.white, Vector4.zero, Image.Type.Simple)
                                        .SetRectTransform(0, -(132 + 4 + 132), 132, -(132 + 4), 0, 1, 0, 1, 0, 1)
                                        .SetOnClick(() => SaveFrameRemoveIcon(2))
                                        .SetTransitionColors(new Color(0.2f, 0.2f, 0.2f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.222f, 0.667f, 1.0f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.5f, 0.5f, 0.5f, 1.0f), 1.0f, 0.1f)
                                        .Element("Image")
                                            .SetRectTransform(0, 0, 0, 0, 0.5f, 0.5f, 0, 0, 1, 1)
                                            .Component_Image(_tool.iconEmpty.Sprite, Color.white, Image.Type.Sliced, Vector4.zero)
                                            .Keep(out _saveFrameIconImages[2])
                                        .Done
                                    .Done
                                    .Element_Button("Icon 4 Button", _tool.iconBlack.Sprite, Color.white, Vector4.zero, Image.Type.Simple)
                                        .SetRectTransform(132 + 4, -(132 + 4 + 132), 132 + 4 + 132, -(132 + 4), 0, 1, 0, 1, 0, 1)
                                        .SetOnClick(() => SaveFrameRemoveIcon(3))
                                        .SetTransitionColors(new Color(0.2f, 0.2f, 0.2f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.222f, 0.667f, 1.0f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.5f, 0.5f, 0.5f, 1.0f), 1.0f, 0.1f)
                                        .Element("Image")
                                            .SetRectTransform(0, 0, 0, 0, 0.5f, 0.5f, 0, 0, 1, 1)
                                            .Component_Image(_tool.iconEmpty.Sprite, Color.white, Image.Type.Sliced, Vector4.zero)
                                            .Keep(out _saveFrameIconImages[3])
                                        .Done
                                    .Done
                                    .Element("Preview")
                                        .SetRectTransform(132 + 4 + 132 + 10 + 64 - 50, -(132 + 5 - 60), 132 + 4 + 132 + 10 + 64 - 50, -(132 + 5 - 60), 0, 1, 0, 1, 0, 1)
                                        .SetSizeDelta(100, 120)
                                        .Keep(out _saveFramePreviewContainer)
                                    .Done
                                .Done
                                .Element("Name Row")
                                    .Layout()
                                        .MinHeight(40)
                                        .FlexibleHeight(0)
                                    .Done
                                    .Do(builder =>
                                    {
                                        var gameObject = Object.Instantiate(_tool.prefabBlueprintNameInputField.Prefab, builder.GameObject.transform);
                                        _saveFrameNameInputField = gameObject.GetComponentInChildren<TMP_InputField>();
                                        if (_saveFrameNameInputField == null) throw new System.Exception("TextMeshPro Input field not found.");
                                        _saveFrameNameInputField.text = "";
                                        _saveFrameNameInputField.onValueChanged.AddListener(new UnityAction<string>((string value) =>
                                        {
                                            if (_saveFramePreviewLabel != null) _saveFramePreviewLabel.text = Path.GetFileName(value);
                                        }));
                                        EventSystem.current.SetSelectedGameObject(_saveFrameNameInputField.gameObject, null);
                                    })
                                .Done
                                .Element("Row Buttons")
                                    .Layout()
                                        .MinHeight(40)
                                        .FlexibleHeight(0)
                                    .Done
                                    .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                    .Element_TextButton("Button Confirm", "Save Blueprint")
                                        .Updater<Button>(_guiUpdaters, () => !string.IsNullOrWhiteSpace(_saveFrameNameInputField?.text))
                                        .SetOnClick(_tool.FinishSaveBlueprint)
                                    .Done
                                    .Element_TextButton("Button Get Existing", "Get Existing")
                                        .SetOnClick(GetInfoFromExistingBlueprint)
                                    .Done
                                    .Element_TextButton("Button Cancel", "Cancel")
                                        .SetOnClick(() => Hide())
                                    .Done
                                .Done
                                .Element_ScrollBox("Material Report ScrollBox", builder =>
                                {
                                    builder = builder
                                        .SetVerticalLayout(new RectOffset(5, 5, 5, 5), 10.0f, TextAnchor.UpperLeft, false, true, true, false, false, false, false)
                                        .Element("Material Report")
                                            .SetRectTransform(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1.0f, 0.0f, 1.0f, 0.0f, 1.0f)
                                            .AutoSize(ContentSizeFitter.FitMode.PreferredSize, ContentSizeFitter.FitMode.PreferredSize)
                                            .Component_Text("", "OpenSansSemibold SDF", 14.0f, Color.white, TextAlignmentOptions.TopLeft)
                                            .Keep(out _saveFrameMaterialReportText)
                                        .Done;
                                })
                                    .Layout()
                                        .PreferredHeight(400)
                                    .Done
                                .Done
                            .Done
                        .Done
                    .Done
                .End();

                FillSaveGrid();
            }

            if (_tool.CurrentBlueprint != null)
            {
                if (_saveFrameNameInputField != null) _saveFrameNameInputField.text = _tool.CurrentBlueprint.Name;

                for (int i = 0; i < 4; i++) _saveFrameIconItemTemplates[i] = null;
                _tool.CurrentBlueprint.IconItemTemplates.CopyTo(_saveFrameIconItemTemplates, 0);
                _saveFrameIconCount = _tool.CurrentBlueprint.IconItemTemplates.Length;
            }

            FillSavePreview();
            FillSaveFrameIcons();
            FillSaveMaterialReport();

            Shown();
        }

        private void FillSaveMaterialReport()
        {
            int totalItemCount = 0;
            var materialReportBuilder = new System.Text.StringBuilder();
            foreach (var kv in _tool.CurrentBlueprint.ShoppingList)
            {
                var itemCount = kv.Value.count;
                if (itemCount > 0)
                {
                    totalItemCount += itemCount;
                    var name = kv.Value.name;
                    materialReportBuilder.AppendLine($"<color=#CCCCCC>{name}:</color> {itemCount}");
                }
            }

            if (totalItemCount > 0)
            {
                materialReportBuilder.AppendLine($"<color=#CCCCCC>Total:</color> {totalItemCount}");
            }

            _saveFrameMaterialReportText.text = materialReportBuilder.ToString();
        }

        internal void FillSavePreview()
        {
            _saveFramePreviewIconImages[0] = _saveFramePreviewIconImages[1] = _saveFramePreviewIconImages[2] = _saveFramePreviewIconImages[3] = null;

            switch (_saveFrameIconCount)
            {
                case 0:
                    {
                        _saveFramePreviewContainer.transform.DestroyAllChildren();
                        var gameObject = UnityEngine.Object.Instantiate(_tool.prefabBlueprintButtonDefaultIcon.Prefab, _saveFramePreviewContainer.transform);
                        var deleteButton = gameObject.transform.Find("DeleteButton")?.gameObject;
                        if (deleteButton != null) deleteButton.SetActive(false);
                        var renameButton = gameObject.transform.Find("RenameButton")?.gameObject;
                        if (renameButton != null) renameButton.SetActive(false);
                        _saveFramePreviewLabel = gameObject.GetComponentInChildren<TextMeshProUGUI>();
                        if (_saveFramePreviewLabel == null) throw new System.ArgumentNullException(nameof(_saveFramePreviewLabel));
                    }
                    break;

                case 1:
                    {
                        _saveFramePreviewContainer.transform.DestroyAllChildren();
                        var gameObject = UnityEngine.Object.Instantiate(_tool.prefabBlueprintButton1Icon.Prefab, _saveFramePreviewContainer.transform);
                        var deleteButton = gameObject.transform.Find("DeleteButton")?.gameObject;
                        if (deleteButton != null) deleteButton.SetActive(false);
                        var renameButton = gameObject.transform.Find("RenameButton")?.gameObject;
                        if (renameButton != null) renameButton.SetActive(false);
                        _saveFramePreviewLabel = gameObject.GetComponentInChildren<TextMeshProUGUI>();
                        if (_saveFramePreviewLabel == null) throw new System.ArgumentNullException(nameof(_saveFramePreviewLabel));
                        _saveFramePreviewIconImages[0] = gameObject.transform.Find("Icon1")?.GetComponent<Image>();
                    }
                    break;

                case 2:
                    {
                        _saveFramePreviewContainer.transform.DestroyAllChildren();
                        var gameObject = UnityEngine.Object.Instantiate(_tool.prefabBlueprintButton2Icon.Prefab, _saveFramePreviewContainer.transform);
                        var deleteButton = gameObject.transform.Find("DeleteButton")?.gameObject;
                        if (deleteButton != null) deleteButton.SetActive(false);
                        var renameButton = gameObject.transform.Find("RenameButton")?.gameObject;
                        if (renameButton != null) renameButton.SetActive(false);
                        _saveFramePreviewLabel = gameObject.GetComponentInChildren<TextMeshProUGUI>();
                        if (_saveFramePreviewLabel == null) throw new System.ArgumentNullException(nameof(_saveFramePreviewLabel));
                        _saveFramePreviewIconImages[0] = gameObject.transform.Find("Icon1")?.GetComponent<Image>();
                        _saveFramePreviewIconImages[1] = gameObject.transform.Find("Icon2")?.GetComponent<Image>();
                    }
                    break;

                case 3:
                    {
                        _saveFramePreviewContainer.transform.DestroyAllChildren();
                        var gameObject = UnityEngine.Object.Instantiate(_tool.prefabBlueprintButton3Icon.Prefab, _saveFramePreviewContainer.transform);
                        var deleteButton = gameObject.transform.Find("DeleteButton")?.gameObject;
                        if (deleteButton != null) deleteButton.SetActive(false);
                        var renameButton = gameObject.transform.Find("RenameButton")?.gameObject;
                        if (renameButton != null) renameButton.SetActive(false);
                        _saveFramePreviewLabel = gameObject.GetComponentInChildren<TextMeshProUGUI>();
                        if (_saveFramePreviewLabel == null) throw new System.ArgumentNullException(nameof(_saveFramePreviewLabel));
                        _saveFramePreviewIconImages[0] = gameObject.transform.Find("Icon1")?.GetComponent<Image>();
                        _saveFramePreviewIconImages[1] = gameObject.transform.Find("Icon2")?.GetComponent<Image>();
                        _saveFramePreviewIconImages[2] = gameObject.transform.Find("Icon3")?.GetComponent<Image>();
                    }
                    break;

                case 4:
                    {
                        _saveFramePreviewContainer.transform.DestroyAllChildren();
                        var gameObject = UnityEngine.Object.Instantiate(_tool.prefabBlueprintButton4Icon.Prefab, _saveFramePreviewContainer.transform);
                        _saveFramePreviewLabel = gameObject.GetComponentInChildren<TextMeshProUGUI>();
                        if (_saveFramePreviewLabel == null) throw new System.ArgumentNullException(nameof(_saveFramePreviewLabel));
                        var renameButton = gameObject.transform.Find("RenameButton")?.gameObject;
                        if (renameButton != null) renameButton.SetActive(false);
                        _saveFramePreviewIconImages[0] = gameObject.transform.Find("Icon1")?.GetComponent<Image>();
                        _saveFramePreviewIconImages[1] = gameObject.transform.Find("Icon2")?.GetComponent<Image>();
                        _saveFramePreviewIconImages[2] = gameObject.transform.Find("Icon3")?.GetComponent<Image>();
                        _saveFramePreviewIconImages[3] = gameObject.transform.Find("Icon4")?.GetComponent<Image>();
                    }
                    break;

                default:
                    break;
            }

            if (_saveFramePreviewLabel != null && _saveFrameNameInputField != null)
            {
                _saveFramePreviewLabel.text = Path.GetFileName(_saveFrameNameInputField.text);
            }

            for (int i = 0; i < _saveFrameIconCount; i++)
            {
                if (_saveFramePreviewIconImages[i] != null)
                {
                    _saveFramePreviewIconImages[i].sprite = _saveFrameIconItemTemplates[i]?.icon ?? _tool.iconEmpty.Sprite;
                }
            }
        }

        internal void FillSaveGrid()
        {
            if (_saveGridObject == null) return;

            _saveGridObject.transform.DestroyAllChildren();

            foreach (var kv in ItemTemplateManager.getAllItemTemplates())
            {
                var itemTemplate = kv.Value;
                if (itemTemplate.isHiddenItem) continue;

                var gameObject = UnityEngine.Object.Instantiate(_tool.prefabBlueprintButtonIcon.Prefab, _saveGridObject.transform);

                var iconImage = gameObject.transform.Find("Icon1")?.GetComponent<Image>();
                if (iconImage != null) iconImage.sprite = itemTemplate.icon;

                var button = gameObject.GetComponentInChildren<Button>();
                if (button != null)
                {
                    button.onClick.AddListener(new UnityAction(() => SaveFrameAddIcon(itemTemplate)));
                }

                var panel = gameObject.GetComponent<Image>();
                if (panel != null) panel.color = Color.clear;
            }
        }

        internal void FillSaveFrameIcons()
        {
            for (int i = 0; i < _saveFrameIconCount; i++)
            {
                if (_saveFrameIconImages[i] != null)
                {
                    _saveFrameIconImages[i].sprite = _saveFrameIconItemTemplates[i]?.icon_256 ?? _tool.iconEmpty.Sprite;
                }
            }
            for (int i = _saveFrameIconCount; i < 4; i++)
            {
                if (_saveFrameIconImages[i] != null)
                {
                    _saveFrameIconImages[i].sprite = _tool.iconEmpty.Sprite;
                }
            }
        }

        private void SaveFrameAddIcon(ItemTemplate itemTemplate)
        {
            if (itemTemplate == null) throw new System.ArgumentNullException(nameof(itemTemplate));
            if (_saveFrameIconCount >= 4) return;

            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIButtonClick);

            _saveFrameIconItemTemplates[_saveFrameIconCount] = itemTemplate;
            _saveFrameIconCount++;

            FillSavePreview();
            FillSaveFrameIcons();
        }

        private void SaveFrameRemoveIcon(int iconIndex)
        {
            if (iconIndex >= _saveFrameIconCount) return;

            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIButtonClick);

            for (int i = iconIndex; i < 3; i++) _saveFrameIconItemTemplates[i] = _saveFrameIconItemTemplates[i + 1];
            _saveFrameIconItemTemplates[3] = null;
            _saveFrameIconCount--;

            FillSavePreview();
            FillSaveFrameIcons();
        }

        private void GetInfoFromExistingBlueprint()
        {
            _tool.ShowLibraryFrame(this);
        }
    }
}
