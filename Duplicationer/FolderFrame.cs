using System.IO;
using TMPro;
using Unfoundry;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Duplicationer
{
    internal class FolderFrame : BaseFrame
    {
        private GameObject folderGridObject = null;
        private GameObject folderFramePreviewContainer = null;
        private Image folderFrameIconImage = null;
        private Image folderFramePreviewIconImage = null;
        private TMP_InputField folderFrameNameInputField = null;
        private TextMeshProUGUI folderFramePreviewLabel = null;
        private ItemTemplate folderFrameIconItemTemplate = null;

        public FolderFrame(BlueprintToolCHM tool) : base(tool) { }

        public void Show(string relativePath, string folderName)
        {
            if (IsOpen) return;

            _tool.HideBlueprintFrame(true);
            _tool.HideSaveFrame(true);

            var originalPath = Path.Combine(DuplicationerPlugin.BlueprintFolder, relativePath, folderName);

            if (_frameRoot != null)
            {
                Object.Destroy(_frameRoot);
                _frameRoot = null;
            }

            ulong usernameHash = GameRoot.getClientCharacter().usernameHash;
            UIBuilder.BeginWith(GameRoot.getDefaultCanvas())
                .Element_Panel("Folder Frame", "corner_cut_outline", new Color(0.133f, 0.133f, 0.133f, 1.0f), new Vector4(13, 10, 8, 13))
                    .Keep(out _frameRoot)
                    .SetRectTransform(100, 100, -100, -100, 0.5f, 0.5f, 0, 0, 1, 1)
                    .Element_Header("HeaderBar", "corner_cut_outline", new Color(0.0f, 0.6f, 1.0f, 1.0f), new Vector4(13, 3, 8, 13))
                        .SetRectTransform(0.0f, -60.0f, 0.0f, 0.0f, 0.5f, 1.0f, 0.0f, 1.0f, 1.0f, 1.0f)
                        .Element("Heading")
                            .SetRectTransform(0.0f, 0.0f, -60.0f, 0.0f, 0.0f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                            .Component_Text($"Folder - /{relativePath}", "OpenSansSemibold SDF", 34.0f, Color.white)
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
                                    var gameObject = Object.Instantiate(_tool.prefabGridScrollView.Prefab, builder.GameObject.transform);
                                    var grid = gameObject.GetComponentInChildren<GridLayoutGroup>();
                                    if (grid == null) throw new System.Exception("Grid not found.");
                                    folderGridObject = grid.gameObject;
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
                                    .SetRectTransform(0, -270, 270, 0, 0, 1, 0, 1, 0, 1)
                                    .SetOnClick(() => {
                                        folderFrameIconImage.sprite = _tool.iconEmpty.Sprite;
                                        folderFrameIconItemTemplate = null;
                                        FillFolderPreview();
                                        AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIButtonClick);
                                    })
                                    .SetTransitionColors(new Color(0.2f, 0.2f, 0.2f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.222f, 0.667f, 1.0f, 1.0f), new Color(0.0f, 0.6f, 1.0f, 1.0f), new Color(0.5f, 0.5f, 0.5f, 1.0f), 1.0f, 0.1f)
                                    .Element("Image")
                                        .SetRectTransform(0, 0, 0, 0, 0.5f, 0.5f, 0, 0, 1, 1)
                                        .Component_Image(_tool.iconEmpty.Sprite, Color.white, Image.Type.Sliced, Vector4.zero)
                                        .Keep(out folderFrameIconImage)
                                    .Done
                                .Done
                                .Element("Preview")
                                    .SetRectTransform(132 + 4 + 132 + 10 + 64 - 50, -(132 + 5 - 60), 132 + 4 + 132 + 10 + 64 - 50, -(132 + 5 - 60), 0, 1, 0, 1, 0, 1)
                                    .SetSizeDelta(100, 120)
                                    .Keep(out folderFramePreviewContainer)
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
                                    folderFrameNameInputField = gameObject.GetComponentInChildren<TMP_InputField>();
                                    if (folderFrameNameInputField == null) throw new System.Exception("TextMeshPro Input field not found.");
                                    folderFrameNameInputField.text = "";
                                    folderFrameNameInputField.onValueChanged.AddListener(new UnityAction<string>((string value) =>
                                    {
                                        if (folderFramePreviewLabel != null) folderFramePreviewLabel.text = Path.GetFileName(value);
                                    }));
                                    EventSystem.current.SetSelectedGameObject(folderFrameNameInputField.gameObject, null);
                                })
                            .Done
                            .Element("Row Buttons")
                                .Layout()
                                    .MinHeight(40)
                                    .FlexibleHeight(0)
                                .Done
                                .SetHorizontalLayout(new RectOffset(0, 0, 0, 0), 5.0f, TextAnchor.UpperLeft, false, true, true, false, true, false, false)
                                .Element_TextButton("Button Confirm", "Confirm")
                                    .Updater<Button>(_guiUpdaters, () => !string.IsNullOrWhiteSpace(folderFrameNameInputField?.text))
                                    .SetOnClick(() => { ConfirmFolderEdit(relativePath, folderName, folderFrameNameInputField?.text); })
                                .Done
                                .Element_TextButton("Button Cancel", "Cancel")
                                    .SetOnClick(() => Hide())
                                .Done
                            .Done
                        .Done
                    .Done
                .Done
            .End();

            FillFolderGrid();

            if (folderFrameNameInputField != null) folderFrameNameInputField.text = folderName;

            folderFrameIconItemTemplate = null;

            if (!string.IsNullOrEmpty(folderName))
            {
                string iconNamePath = Path.Combine(originalPath, "__folder_icon.txt");
                if (File.Exists(iconNamePath))
                {
                    var iconName = File.ReadAllText(iconNamePath).Trim();
                    var iconHash = ItemTemplate.generateStringHash(iconName);
                    folderFrameIconItemTemplate = ItemTemplateManager.getItemTemplate(iconHash);
                }
            }

            FillFolderPreview();
            FillFolderFrameIcons();

            Shown();
        }

        private void ConfirmFolderEdit(string relativePath, string originalName, string newName)
        {
            var newPath = Path.Combine(DuplicationerPlugin.BlueprintFolder, relativePath, newName);
            var iconNamePath = Path.Combine(newPath, "__folder_icon.txt");
            if (string.IsNullOrWhiteSpace(originalName))
            {
                try
                {
                    Directory.CreateDirectory(newPath);
                    if (Directory.Exists(newPath))
                    {
                        if (folderFrameIconItemTemplate != null)
                        {
                            File.WriteAllText(iconNamePath, folderFrameIconItemTemplate.identifier);
                        }

                        Hide();
                        _tool.FillLibraryGrid(relativePath);
                    }
                }
                catch { }
            }
            else
            {
                var originalPath = Path.Combine(DuplicationerPlugin.BlueprintFolder, relativePath, originalName);

                try
                {
                    if (originalPath != newPath && Directory.Exists(originalPath) && !Directory.Exists(newPath))
                    {
                        Directory.Move(originalPath, newPath);
                    }

                    if (File.Exists(iconNamePath)) File.Delete(iconNamePath);

                    if (folderFrameIconItemTemplate != null)
                    {
                        File.WriteAllText(iconNamePath, folderFrameIconItemTemplate.identifier);
                    }

                    Hide();
                    _tool.FillLibraryGrid(relativePath);
                }
                catch { }
            }
        }

        private void FillFolderGrid()
        {
            if (folderGridObject == null) return;

            folderGridObject.transform.DestroyAllChildren();

            foreach (var kv in ItemTemplateManager.getAllItemTemplates())
            {
                var itemTemplate = kv.Value;
                if (itemTemplate.isHiddenItem) continue;

                var gameObject = Object.Instantiate(_tool.prefabBlueprintButtonIcon.Prefab, folderGridObject.transform);

                var iconImage = gameObject.transform.Find("Icon1")?.GetComponent<Image>();
                if (iconImage != null) iconImage.sprite = itemTemplate.icon;

                var button = gameObject.GetComponentInChildren<Button>();
                if (button != null)
                {
                    button.onClick.AddListener(new UnityAction(() => {
                        AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIButtonClick);
                        folderFrameIconItemTemplate = itemTemplate;
                        folderFrameIconImage.sprite = itemTemplate?.icon ?? _tool.iconEmpty.Sprite;
                        FillFolderPreview();
                    }));
                }

                var panel = gameObject.GetComponent<Image>();
                if (panel != null) panel.color = Color.clear;
            }
        }

        private void FillFolderPreview()
        {
            folderFramePreviewContainer.transform.DestroyAllChildren();
            var gameObject = Object.Instantiate(_tool.prefabBlueprintButtonFolder.Prefab, folderFramePreviewContainer.transform);
            var deleteButton = gameObject.transform.Find("DeleteButton")?.gameObject;
            if (deleteButton != null) deleteButton.SetActive(false);
            var renameButton = gameObject.transform.Find("RenameButton")?.gameObject;
            if (renameButton != null) renameButton.SetActive(false);
            folderFramePreviewLabel = gameObject.GetComponentInChildren<TextMeshProUGUI>();
            folderFramePreviewIconImage = gameObject.transform.Find("Icon1")?.GetComponent<Image>();

            if (folderFramePreviewLabel != null && folderFrameNameInputField != null)
            {
                folderFramePreviewLabel.text = Path.GetFileName(folderFrameNameInputField.text);
            }

            if (folderFramePreviewIconImage != null)
            {
                folderFramePreviewIconImage.sprite = folderFrameIconItemTemplate?.icon ?? _tool.iconEmpty.Sprite;
            }
        }

        private void FillFolderFrameIcons()
        {
            if (folderFrameIconImage != null)
            {
                folderFrameIconImage.sprite = folderFrameIconItemTemplate?.icon_256 ?? _tool.iconEmpty.Sprite;
            }
        }
    }
}
