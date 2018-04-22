﻿#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace ModIO
{
    // TODO(@jackson): Implement Login Dialog
    // TODO(@jackson): Needs beauty-pass
    // TODO(@jackson): Force repaint on Callbacks
    // TODO(@jackson): Implement client-side error-checking in submission
    // TODO(@jackson): Check if undos are necessary

    [CustomEditor(typeof(ScriptableModProfile))]
    public class ModProfileEditor : Editor
    {
        // ------[ SERIALIZED PROPERTIES ]------
        private SerializedProperty modIdProperty;
        private SerializedProperty editableModProfileProperty;

        // ------[ EDITOR CACHING ]------
        private ModProfile profile;

        // ------[ VIEW INFORMATION ]------
        private IModProfileViewPart[] profileViewParts;
        protected Vector2 scrollPos;
        protected bool isRepaintRequired;
        // Profile Initialization
        private int modInitializationOptionIndex;
        private ModProfile[] modList;
        private string[] modOptions;

        // ------[ INITIALIZATION ]------
        protected virtual void OnEnable()
        {
            ModManager.Initialize();

            // Grab Serialized Properties
            serializedObject.Update();
            modIdProperty = serializedObject.FindProperty("modId");
            editableModProfileProperty = serializedObject.FindProperty("editableModProfile");

            profileViewParts = CreateProfileViewParts();

            // Profile Initialization
            if(modIdProperty.intValue < 0)
            {
                profile = null;

                // TODO(@jackson): Filter by editable
                modInitializationOptionIndex = 0;
                modList = ModManager.GetAllModProfiles();
                modOptions = new string[modList.Length];
                for(int i = 0; i < modList.Length; ++i)
                {
                    ModProfile mod = modList[i];
                    modOptions[i] = mod.name;
                }
            }
            else
            {
                // Initialize View
                profile = ModManager.GetModProfile(modIdProperty.intValue);

                foreach(IModProfileViewPart viewPart in profileViewParts)
                {
                    viewPart.OnEnable(editableModProfileProperty, profile);
                }
            }

            scrollPos = Vector2.zero;

            // Events
            EditorApplication.update += OnUpdate;
        }

        protected virtual void OnDisable()
        {
            foreach(IModProfileViewPart viewPart in profileViewParts)
            {
                viewPart.OnDisable();
            }

            EditorApplication.update -= OnUpdate;
        }

        protected virtual IModProfileViewPart[] CreateProfileViewParts()
        {
            return new IModProfileViewPart[]
            {
                new ModProfileInfoViewPart(),
                new ModMediaViewPart(),
            };
        }

        // ------[ GUI ]------
        public override void OnInspectorGUI()
        {
            if(serializedObject.FindProperty("modId").intValue < 0)
            {
                LayoutProfileInitialization();
            }
            else
            {
                serializedObject.Update();
                foreach(IModProfileViewPart viewPart in profileViewParts)
                {
                    viewPart.OnGUI();
                }
                serializedObject.ApplyModifiedProperties();
            }

            isRepaintRequired = false;
        }

        protected virtual void LayoutProfileInitialization()
        {
            EditorGUILayout.LabelField("Initialize Mod Profile");

            // ---[ DISPLAY ]---
            EditorGUILayout.Space();

            if(GUILayout.Button("Create New"))
            {
                EditorApplication.delayCall += () =>
                {
                    ScriptableModProfile smp = this.target as ScriptableModProfile;
                    Undo.RecordObject(smp, "Initialize Mod Profile");
                    
                    smp.modId = 0;
                    smp.editableModProfile = new EditableModProfile();

                    OnDisable();
                    OnEnable();
                    isRepaintRequired = true;
                };
            }

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("---- OR ----");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Load Existing Profile");

            if(modList.Length > 0)
            {
                modInitializationOptionIndex = EditorGUILayout.Popup("Select Mod", modInitializationOptionIndex, modOptions, null);
                if(GUILayout.Button("Load"))
                {
                    ModProfile profile = modList[modInitializationOptionIndex];
                    EditorApplication.delayCall += () =>
                    {
                        ScriptableModProfile smp = this.target as ScriptableModProfile;
                        Undo.RecordObject(smp, "Initialize Mod Profile");
                        
                        smp.modId = profile.id;
                        smp.editableModProfile = EditableModProfile.CreateFromProfile(profile);

                        OnDisable();
                        OnEnable();
                        isRepaintRequired = true;
                    };
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No loadable mod profiles detected.",
                                        MessageType.Info);
            }
        }

        // ------[ UPDATE ]------
        public virtual void OnUpdate()
        {
            foreach(IModProfileViewPart viewPart in profileViewParts)
            {
                viewPart.OnUpdate();
                isRepaintRequired |= viewPart.IsRepaintRequired();
            }

            if(isRepaintRequired)
            {
                Repaint();
            }
        }
    }
}

#endif
