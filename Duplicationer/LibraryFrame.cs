using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using TMPro;
using Unfoundry;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Duplicationer
{
    internal class LibraryFrame : BaseFrame, IEscapeCloseable
    {
        private TextMeshProUGUI libraryFrameHeading = null;
        private GameObject libraryGridObject = null;
        private string lastLibraryRelativePath = "";

        public LibraryFrame(BlueprintToolCHM tool) : base(tool) { }

        public void Show(SaveFrame saveFrame = null)
        {
            if (saveFrame == null) _tool.HideSaveFrame(true);

            _tool.HideBlueprintFrame(true);
            _tool.HideFolderFrame(true);

            if (_frameRoot != null)
            {
                if (_frameRoot.activeSelf) return;

                Object.Destroy(_frameRoot);
                _frameRoot = null;
            }

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
                .Element_Panel("Library Frame", "corner_cut_outline", new Color(0.133f, 0.133f, 0.133f, 1.0f), new Vector4(13, 10, 8, 13))
                    .Keep(out _frameRoot)
                    .SetRectTransform(100, 100, -100, -100, 0.5f, 0.5f, 0, 0, 1, 1)
                    .Element_Header("HeaderBar", "corner_cut_outline", new Color(0.0f, 0.6f, 1.0f, 1.0f), new Vector4(13, 3, 8, 13))
                        .SetRectTransform(0.0f, -60.0f, 0.0f, 0.0f, 0.5f, 1.0f, 0.0f, 1.0f, 1.0f, 1.0f)
                        .Element("Heading")
                            .SetRectTransform(0.0f, 0.0f, -60.0f, 0.0f, 0.0f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                            .Component_Text("Blueprints", "OpenSansSemibold SDF", 34.0f, Color.white)
                            .Keep(out libraryFrameHeading)
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
                        .Element("Padding")
                            .SetRectTransform(10.0f, 10.0f, -10.0f, -10.0f, 0.5f, 0.5f, 0.0f, 0.0f, 1.0f, 1.0f)
                            .Do(builder =>
                            {
                                var gameObject = Object.Instantiate(_tool.prefabGridScrollView.Prefab, builder.GameObject.transform);
                                var grid = gameObject.GetComponentInChildren<GridLayoutGroup>();
                                if (grid == null) throw new System.Exception("Grid not found.");
                                libraryGridObject = grid.gameObject;
                            })
                        .Done
                    .Done
                .Done
            .End();

            FillLibraryGrid(lastLibraryRelativePath, saveFrame);

            Shown();
        }

        internal void FillLibraryGrid(string relativePath, SaveFrame saveFrame = null)
        {
            if (libraryGridObject == null) return;

            lastLibraryRelativePath = relativePath;

            libraryGridObject.transform.DestroyAllChildren();

            libraryFrameHeading.text = string.IsNullOrEmpty(relativePath) ? "Blueprints" : $"Blueprints\\{relativePath}";

            var prefabs = new GameObject[5]
            {
                _tool.prefabBlueprintButtonDefaultIcon.Prefab, _tool.prefabBlueprintButton1Icon.Prefab, _tool.prefabBlueprintButton2Icon.Prefab, _tool.prefabBlueprintButton3Icon.Prefab, _tool.prefabBlueprintButton4Icon.Prefab
            };

            if (!string.IsNullOrEmpty(relativePath))
            {
                var backGameObject = Object.Instantiate(_tool.prefabBlueprintButtonFolderBack.Prefab, libraryGridObject.transform);
                var backButton = backGameObject.GetComponentInChildren<Button>();
                if (backButton != null)
                {
                    backButton.onClick.AddListener(new UnityAction(() =>
                    {
                        AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIButtonClick);

                        var backPath = Path.GetDirectoryName(relativePath);
                        FillLibraryGrid(backPath, saveFrame);
                    }));
                }
            }

            if (saveFrame == null)
            {
                var newFolderGameObject = Object.Instantiate(_tool.prefabBlueprintButtonFolderNew.Prefab, libraryGridObject.transform);
                var newFolderButton = newFolderGameObject.GetComponentInChildren<Button>();
                if (newFolderButton != null)
                {
                    newFolderButton.onClick.AddListener(new UnityAction(() =>
                    {
                        _tool.ShowFolderFrame(relativePath, "");
                    }));
                }
            }

            var builder = UIBuilder.BeginWith(libraryGridObject);
            foreach (var path in Directory.GetDirectories(Path.Combine(DuplicationerPlugin.BlueprintFolder, relativePath)))
            {
                var name = Path.GetFileName(path);

                var gameObject = Object.Instantiate(_tool.prefabBlueprintButtonFolder.Prefab, libraryGridObject.transform);

                var label = gameObject.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
                if (label != null) label.text = name;

                ItemTemplate iconItemTemplate = null;
                var iconNamePath = Path.Combine(path, "__folder_icon.txt");
                if (File.Exists(iconNamePath))
                {
                    var identifier = File.ReadAllText(iconNamePath).Trim();
                    var hash = ItemTemplate.generateStringHash(identifier);
                    iconItemTemplate = ItemTemplateManager.getItemTemplate(hash);
                }

                var iconImage = gameObject.transform.Find("Icon1")?.GetComponent<Image>();
                if (iconImage != null)
                {
                    iconImage.sprite = iconItemTemplate?.icon ?? _tool.iconEmpty.Sprite;
                }

                var button = gameObject.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.AddListener(new UnityAction(() =>
                    {
                        ActionManager.AddQueuedEvent(() =>
                        {
                            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIButtonClick);

                            FillLibraryGrid(Path.Combine(relativePath, name), saveFrame);
                        });
                    }));
                }

                var deleteButton = gameObject.transform.Find("DeleteButton")?.GetComponent<Button>();
                if (deleteButton != null)
                {
                    if (saveFrame != null)
                    {
                        deleteButton.gameObject.SetActive(false);
                    }
                    else
                    {
                        var nameToDelete = name;
                        var pathToDelete = path;
                        deleteButton.onClick.AddListener(new UnityAction(() =>
                        {
                            ActionManager.AddQueuedEvent(() =>
                            {
                                ConfirmationFrame.Show($"Delete folder '{name}'", "Delete", () =>
                                {
                                    try
                                    {
                                        Directory.Delete(pathToDelete, true);
                                        FillLibraryGrid(relativePath);
                                    }
                                    catch (System.Exception) { }
                                });
                            });
                        }));
                    }

                    var renameButton = gameObject.transform.Find("RenameButton")?.GetComponent<Button>();
                    if (renameButton != null)
                    {
                        if (saveFrame != null)
                        {
                            renameButton.gameObject.SetActive(false);
                        }
                        else
                        {
                            var nameToRename = name;
                            var pathToRename = path;
                            renameButton.onClick.AddListener(new UnityAction(() =>
                            {
                                ActionManager.AddQueuedEvent(() =>
                                {
                                    _tool.ShowFolderFrame(relativePath, nameToRename);
                                });
                            }));
                        }
                    }
                }
            }

            foreach (var path in Directory.GetFiles(Path.Combine(DuplicationerPlugin.BlueprintFolder, relativePath), $"*.{DuplicationerPlugin.BlueprintExtension}"))
            {
                if (Blueprint.TryLoadFileHeader(path, out var header, out var name))
                {
                    var iconItemTemplates = new List<ItemElementTemplate>();
                    if (!string.IsNullOrEmpty(header.icon1))
                    {
                        var template = ItemElementTemplate.Get(header.icon1);
                        if (template.isValid && template.icon != null) iconItemTemplates.Add(template);
                    }
                    if (!string.IsNullOrEmpty(header.icon2))
                    {
                        var template = ItemElementTemplate.Get(header.icon2);
                        if (template.isValid && template.icon != null) iconItemTemplates.Add(template);
                    }
                    if (!string.IsNullOrEmpty(header.icon3))
                    {
                        var template = ItemElementTemplate.Get(header.icon3);
                        if (template.isValid && template.icon != null) iconItemTemplates.Add(template);
                    }
                    if (!string.IsNullOrEmpty(header.icon4))
                    {
                        var template = ItemElementTemplate.Get(header.icon4);
                        if (template.isValid && template.icon != null) iconItemTemplates.Add(template);
                    }

                    int iconCount = iconItemTemplates.Count;

                    var gameObject = Object.Instantiate(prefabs[iconCount], libraryGridObject.transform);

                    var label = gameObject.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
                    if (label != null) label.text = name;

                    var iconImages = new Image[] {
                        gameObject.transform.Find("Icon1")?.GetComponent<Image>(),
                        gameObject.transform.Find("Icon2")?.GetComponent<Image>(),
                        gameObject.transform.Find("Icon3")?.GetComponent<Image>(),
                        gameObject.transform.Find("Icon4")?.GetComponent<Image>()
                    };

                    for (int iconIndex = 0; iconIndex < iconCount; iconIndex++)
                    {
                        iconImages[iconIndex].sprite = iconItemTemplates[iconIndex].icon;
                    }

                    var button = gameObject.GetComponent<Button>();
                    if (button != null)
                    {
                        if (saveFrame != null)
                        {
                            var nameForSaveInfo = Path.Combine(relativePath, Path.GetFileNameWithoutExtension(path));
                            button.onClick.AddListener(new UnityAction(() =>
                            {
                                ActionManager.AddQueuedEvent(() =>
                                {
                                    saveFrame.BlueprintName = nameForSaveInfo;
                                    saveFrame.IconCount = iconCount;
                                    for (int i = 0; i < 4; i++)
                                    {
                                        saveFrame.IconItemTemplates[i] = (i < iconCount) ? iconItemTemplates[i] : ItemElementTemplate.Empty;
                                    }
                                    saveFrame.FillSaveFrameIcons();
                                    saveFrame.FillSavePreview();
                                    Hide();
                                });
                            }));
                        }
                        else
                        {
                            button.onClick.AddListener(new UnityAction(() =>
                            {
                                ActionManager.AddQueuedEvent(() =>
                                {
                                    Hide();
                                    _tool.ClearBlueprintPlaceholders();
                                    _tool.LoadBlueprintFromFile(path);
                                    _tool.SelectMode(_tool.modePlace);
                                });
                            }));
                        }
                    }

                    var deleteButton = gameObject.transform.Find("DeleteButton")?.GetComponent<Button>();
                    if (deleteButton != null)
                    {
                        if (saveFrame != null)
                        {
                            deleteButton.gameObject.SetActive(false);
                        }
                        else
                        {
                            var nameToDelete = name;
                            var pathToDelete = path;
                            deleteButton.onClick.AddListener(new UnityAction(() =>
                            {
                                ActionManager.AddQueuedEvent(() =>
                                {
                                    ConfirmationFrame.Show($"Delete '{name}'", "Delete", () =>
                                    {
                                        try
                                        {
                                            File.Delete(pathToDelete);
                                            FillLibraryGrid(relativePath);
                                        }
                                        catch (System.Exception) { }
                                    });
                                });
                            }));
                        }

                        var renameButton = gameObject.transform.Find("RenameButton")?.GetComponent<Button>();
                        if (renameButton != null)
                        {
                            if (saveFrame != null)
                            {
                                renameButton.gameObject.SetActive(false);
                            }
                            else
                            {
                                var nameToRename = name;
                                var pathToRename = path;
                                renameButton.onClick.AddListener(new UnityAction(() =>
                                {
                                    ActionManager.AddQueuedEvent(() =>
                                    {
                                        TextEntryFrame.Show($"Rename Blueprint", nameToRename, "Rename", (string newName) =>
                                        {
                                            string filenameBase = Path.Combine(Path.GetDirectoryName(newName), PathHelpers.MakeValidFileName(Path.GetFileName(newName)));
                                            string newPath = Path.Combine(DuplicationerPlugin.BlueprintFolder, relativePath, $"{filenameBase}.{DuplicationerPlugin.BlueprintExtension}");
                                            if (File.Exists(newPath))
                                            {
                                                ConfirmationFrame.Show($"Overwrite '{newName}'?", "Overwrite", () =>
                                                {
                                                    try
                                                    {
                                                        DuplicationerPlugin.log.Log($"Renaming blueprint '{nameToRename}' to '{newName}'");
                                                        File.Delete(newPath);
                                                        File.Move(pathToRename, newPath);
                                                        RenameBlueprint(newPath, Path.GetFileName(newName));
                                                        FillLibraryGrid(relativePath);
                                                    }
                                                    catch (System.Exception) { }
                                                });
                                            }
                                            else
                                            {
                                                try
                                                {
                                                    DuplicationerPlugin.log.Log($"Renaming blueprint '{nameToRename}' to '{newName}'");
                                                    File.Move(pathToRename, newPath);
                                                    RenameBlueprint(newPath, Path.GetFileName(newName));
                                                    FillLibraryGrid(relativePath);
                                                }
                                                catch (System.Exception) { }
                                            }
                                        });
                                    });
                                }));
                            }
                        }
                    }
                }
            }
        }

        private void RenameBlueprint(string path, string name)
        {
            var iconItemTemplateIds = new ulong[4];

            var reader = new BinaryReader(new FileStream(path, FileMode.Open, FileAccess.Read));

            var magic = reader.ReadUInt32();
            var version = reader.ReadUInt32();

            for (int i = 0; i < 4; i++) iconItemTemplateIds[i] = reader.ReadUInt64();

            var oldName = reader.ReadString();

            var data = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));

            reader.Close();
            reader.Dispose();

            var writer = new BinaryWriter(new FileStream(path, FileMode.Create, FileAccess.Write));

            writer.Write(magic);
            writer.Write(version);

            for (int i = 0; i < 4; i++)
            {
                writer.Write(iconItemTemplateIds[i]);
            }

            writer.Write(name);

            writer.Write(data);

            writer.Close();
            writer.Dispose();
        }

        internal void HideFolderFrame()
        {
            if (_frameRoot == null || !_frameRoot.activeSelf) return;

            AudioManager.playUISoundEffect(ResourceDB.resourceLinker.audioClip_UIClose);

            _frameRoot.SetActive(false);
            GlobalStateManager.removeCursorRequirement();
            GlobalStateManager.deRegisterEscapeCloseable(this);
        }
    }
}
