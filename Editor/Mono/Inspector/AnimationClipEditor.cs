// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using UnityEngine;
using UnityEditor.Animations;
using System;
using System.Collections.Generic;
using System.Linq;
using Object = UnityEngine.Object;
using System.Globalization;
using UnityEditorInternal;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using AnimatorControllerLayer = UnityEditor.Animations.AnimatorControllerLayer;

namespace UnityEditor
{
    [CustomEditor(typeof(AnimationClip))]
    [CanEditMultipleObjects]
    internal class AnimationClipEditor : Editor
    {
        internal static void EditWithImporter(AnimationClip clip)
        {
            ModelImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) as ModelImporter;

            if (importer)
            {
                Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(importer.assetPath);
                ModelImporterEditor inspector = Editor.CreateEditor(importer) as ModelImporterEditor;
                EditorPrefs.SetInt(inspector.GetType().Name + "ActiveEditorIndex", 2);
                Object.DestroyImmediate(inspector);

                int clipIndex = 0;
                //The ModelImporter handles "ClipAnimations" and "DefaultClipAnimations" independently, where there are no user specified clips, this clip must be a default clip.
                var clipAnimations = importer.clipAnimations.Any() ? importer.clipAnimations : importer.defaultClipAnimations;
                for (int i = 0; i < clipAnimations.Length; i++)
                {
                    if (clipAnimations[i].name == clip.name)
                    {
                        clipIndex = i;
                        break;
                    }
                }

                EditorPrefs.SetInt(ModelImporterClipEditor.ActiveClipIndex, clipIndex);
            }
        }

        internal static class Styles
        {
            public static GUIContent StartFrame = EditorGUIUtility.TrTextContent("Start", "Start frame of the clip.");
            public static GUIContent EndFrame = EditorGUIUtility.TrTextContent("End", "End frame of the clip.");
            public static string AverageVelocity = L10n.Tr("Average Velocity: {0}\nAverage Angular Y Speed: {1} deg/s");

            public static GUIContent HasAdditiveReferencePose = EditorGUIUtility.TrTextContent("Additive Reference Pose", "Enable to define the additive reference pose frame.");
            public static GUIContent AdditiveReferencePoseFrame = EditorGUIUtility.TrTextContent("Pose Frame", "Pose Frame.");
            public static GUIContent LoopTime = EditorGUIUtility.TrTextContent("Loop Time", "Enable to make the animation play through and then restart when the end is reached.");
            public static GUIContent LoopPose = EditorGUIUtility.TrTextContent("Loop Pose", "Enable to make the animation loop seamlessly.");
            public static GUIContent LoopCycleOffset = EditorGUIUtility.TrTextContent("Cycle Offset", "Offset to the cycle of a looping animation, if we want to start it at a different time.");
            public static GUIContent RootTransformRotation = EditorGUIUtility.TrTextContent("Root Transform Rotation");
            public static GUIContent RootTransformRotationY = EditorGUIUtility.TrTextContent("Root Transform Position (Y)");
            public static GUIContent RootTransformPositionXZ = EditorGUIUtility.TrTextContent("Root Transform Position (XZ)");

            public static GUIContent BakeIntoPoseOrientation = EditorGUIUtility.TrTextContent("Bake Into Pose", "Enable to make root rotation be baked into the movement of the bones. Disable to make root rotation be stored as root motion.");
            public static GUIContent OrientationOffsetY = EditorGUIUtility.TrTextContent("Offset", "Offset to the root rotation (in degrees).");

            public static GUIContent BasedUponOrientation = EditorGUIUtility.TrTextContent("Based Upon", "What the root rotation is based upon.");
            public static GUIContent BasedUponStartOrientation = EditorGUIUtility.TrTextContent("Based Upon (at Start)", "What the root rotation is based upon.");

            public static GUIContent[] BasedUponRotationHumanOpt =
            {
                EditorGUIUtility.TrTextContent("Original", "Keeps the rotation as it is authored in the source file."),
                EditorGUIUtility.TrTextContent("Body Orientation", "Keeps the upper body pointing forward.")
            };

            public static GUIContent[] BasedUponRotationOpt =
            {
                EditorGUIUtility.TrTextContent("Original", "Keeps the rotation as it is authored in the source file."),
                EditorGUIUtility.TrTextContent("Root Node Rotation", "Keeps the upper body pointing forward.")
            };

            public static GUIContent BakeIntoPosePositionY = EditorGUIUtility.TrTextContent("Bake Into Pose", "Enable to make vertical root motion be baked into the movement of the bones. Disable to make vertical root motion be stored as root motion.");
            public static GUIContent PositionOffsetY = EditorGUIUtility.TrTextContent("Offset", "Offset to the vertical root position.");

            public static GUIContent BasedUponPositionY = EditorGUIUtility.TrTextContent("Based Upon", "What the vertical root position is based upon.");
            public static GUIContent BasedUponStartPositionY = EditorGUIUtility.TrTextContent("Based Upon (at Start)", "What the vertical root position is based upon.");


            public static GUIContent[] BasedUponPositionYHumanOpt =
            {
                EditorGUIUtility.TrTextContent("Original", "Keeps the vertical position as it is authored in the source file."),
                EditorGUIUtility.TrTextContent("Center of Mass", "Keeps the center of mass aligned with root transform position."),
                EditorGUIUtility.TrTextContent("Feet", "Keeps the feet aligned with the root transform position.")
            };

            public static GUIContent[] BasedUponPositionYOpt =
            {
                EditorGUIUtility.TrTextContent("Original", "Keeps the vertical position as it is authored in the source file."),
                EditorGUIUtility.TrTextContent("Root Node Position")
            };

            public static GUIContent BakeIntoPosePositionXZ = EditorGUIUtility.TrTextContent("Bake Into Pose", "Enable to make horizontal root motion be baked into the movement of the bones. Disable to make horizontal root motion be stored as root motion.");

            public static GUIContent BasedUponPositionXZ = EditorGUIUtility.TrTextContent("Based Upon", "What the horizontal root position is based upon.");
            public static GUIContent BasedUponStartPositionXZ = EditorGUIUtility.TrTextContent("Based Upon (at Start)", "What the horizontal root position is based upon.");

            public static GUIContent[] BasedUponPositionXZHumanOpt =
            {
                EditorGUIUtility.TrTextContent("Original", "Keeps the horizontal position as it is authored in the source file."),
                EditorGUIUtility.TrTextContent("Center of Mass", "Keeps the center of mass aligned with root transform position.")
            };

            public static GUIContent[] BasedUponPositionXZOpt =
            {
                EditorGUIUtility.TrTextContent("Original", "Keeps the horizontal position as it is authored in the source file."),
                EditorGUIUtility.TrTextContent("Root Node Position")
            };

            public static GUIContent Mirror = EditorGUIUtility.TrTextContent("Mirror", "Mirror left and right in this clip.");

            public static GUIContent Curves = EditorGUIUtility.TrTextContent("Curves", "Parameter-related curves.");
            public static GUIContent Length = EditorGUIUtility.TrTextContent("Length");
            public static GUIContent AddLoopFrame = EditorGUIUtility.TrTextContent("Add Loop Frame");
            public static GUIContent WrapMode = EditorGUIUtility.TrTextContent("Wrap Mode");
            public static GUIContent Events = EditorGUIUtility.TrTextContent("Events");
            public static GUIContent LoopMatch = EditorGUIUtility.TrTextContent("loop match");

            public static string InvalidMultiSelection = L10n.Tr("Both legacy and non legacy Animation Clips have been selected. This combination cannot be edited together. Select either legacy or non legacy Animation Clips.");

            public static GUIContent AddEventContent = EditorGUIUtility.TrIconContent("Animation.AddEvent", "Add Event.");

            public static GUIContent GreenLightIcon = EditorGUIUtility.IconContent("lightMeter/greenLight");
            public static GUIContent LightRimIcon = EditorGUIUtility.IconContent("lightMeter/lightRim");
            public static GUIContent OrangeLightIcon = EditorGUIUtility.IconContent("lightMeter/orangeLight");
            public static GUIContent RedLightIcon = EditorGUIUtility.IconContent("lightMeter/redLight");

            public static GUIContent PrevKeyContent = EditorGUIUtility.TrIconContent("Animation.PrevKey", "Go to previous key frame.");
            public static GUIContent NextKeyContent = EditorGUIUtility.TrIconContent("Animation.NextKey", "Go to next key frame.");
            public static GUIContent AddKeyframeContent = EditorGUIUtility.TrIconContent("Animation.AddKeyframe", "Add Keyframe.");

            public static GUIContent AddEvent = EditorGUIUtility.TrTextContent("Add Animation Event");
            public static GUIContent DeleteEvents = EditorGUIUtility.TrTextContent("Delete Animation Events");
            public static GUIContent DeleteEvent = EditorGUIUtility.TrTextContent("Delete Animation Event");
            public static GUIContent CopyEvents = EditorGUIUtility.TrTextContent("Copy Animation Events");
            public static GUIContent PasteEvents = EditorGUIUtility.TrTextContent("Paste Animation Events");
        }

        static string s_LoopMeterStr = "LoopMeter";
        static int s_LoopMeterHint = s_LoopMeterStr.GetHashCode();

        static string s_LoopOrientationMeterStr = "LoopOrientationMeter";
        static int s_LoopOrientationMeterHint = s_LoopOrientationMeterStr.GetHashCode();

        static string s_LoopPositionYMeterStr = "LoopPostionYMeter";
        static int s_LoopPositionYMeterHint = s_LoopPositionYMeterStr.GetHashCode();

        static string s_LoopPositionXZMeterStr = "LoopPostionXZMeter";
        static int s_LoopPositionXZMeterHint = s_LoopPositionXZMeterStr.GetHashCode();

        static public float s_EventTimelineMax = 1.05f;

        // Update the ClipInfo if needed.
        // Needed because of the dummy serialized property (m_DefaultClipsSerializedObject) TransferDefaultClipsToCustomClips
        private void UpdateEventsPopupClipInfo(AnimationClipInfoProperties info)
        {
            if (m_EventManipulationHandler != null)
                m_EventManipulationHandler.UpdateEvents(info);
        }

        private AvatarMask m_Mask = null;
        public AvatarMask mask
        {
            get { return m_Mask; }
            set { m_Mask = value; }
        }

        private AnimationClipInfoProperties m_ClipInfo = null;
        public void ShowRange(AnimationClipInfoProperties info)
        {
            UpdateEventsPopupClipInfo(info);
            m_ClipInfo = info;
            info.AssignToPreviewClip(m_Clips[0]);
        }

        public string[] takeNames { get; set; }
        public int takeIndex { get; set; }
        private AnimationClip[] m_Clips = null;
        private AnimatorController m_Controller = null;
        private AnimatorStateMachine m_StateMachine;
        private AnimatorState m_State;

        private AvatarPreview m_AvatarPreview = null;

        private TimeArea m_TimeArea;
        private TimeArea m_EventTimeArea;

        private SerializedProperty m_WrapModeProperty;
        private SerializedProperty m_ClipSettingsProperty;
        private SerializedProperty m_LoopTimeProperty;
        private SerializedProperty m_LoopBlendProperty;
        private SerializedProperty m_CycleOffsetProperty;
        private SerializedProperty m_LoopBlendOrientationProperty;
        private SerializedProperty m_KeepOriginalOrientationProperty;
        private SerializedProperty m_OrientationOffsetYProperty;
        private SerializedProperty m_LoopBlendPositionYProperty;
        private SerializedProperty m_KeepOriginalPositionYProperty;
        private SerializedProperty m_HeightFromFeetProperty;
        private SerializedProperty m_LevelProperty;
        private SerializedProperty m_LoopBlendPositionXZProperty;
        private SerializedProperty m_KeepOriginalPositionXZProperty;
        private SerializedProperty m_MirrorProperty;

        private bool m_DraggingRange = false;
        private bool m_DraggingRangeBegin = false;
        private bool m_DraggingRangeEnd = false;
        private float m_DraggingStartFrame = 0;
        private float m_DraggingStopFrame = 0;
        private float m_DraggingAdditivePoseFrame = 0;

        private bool m_LoopTime = false;
        private bool m_LoopBlend = false;
        private bool m_LoopBlendOrientation = false;
        private bool m_LoopBlendPositionY = false;
        private bool m_LoopBlendPositionXZ = false;
        private float m_StartFrame = 0;
        private float m_StopFrame = 1;
        private float m_AdditivePoseFrame = 0;
        private float m_InitialClipLength = 0;

        static private bool m_ShowCurves = false;

        private EventManipulationHandler m_EventManipulationHandler;
        static private bool m_ShowEvents = false;

        bool m_NeedsToGenerateClipInfo = false;
        bool m_IsSelectingMultipleClips = false;

        public bool needsToGenerateClipInfo
        {
            get { return m_NeedsToGenerateClipInfo; }
            set { m_NeedsToGenerateClipInfo = value; }
        }

        const int kSamplesPerSecond = 60;
        const int kPose = 0;
        const int kRotation = 1;
        const int kHeight = 2;
        const int kPosition = 3;
        Vector2[][][] m_QualityCurves = new Vector2[4][][];
        bool m_DirtyQualityCurves = false;
        bool m_FirstInitialization = true;

        private void InitController()
        {
            if (m_Clips[0].legacy)
                return;

            if (m_AvatarPreview != null && m_AvatarPreview.Animator != null)
            {
                bool wasInitialized = true;
                if (m_Controller == null)
                {
                    m_Controller = new AnimatorController();
                    m_Controller.pushUndo = false;
                    m_Controller.hideFlags = HideFlags.HideAndDontSave;
                    m_Controller.AddLayer("preview");

                    m_StateMachine = m_Controller.layers[0].stateMachine;
                    m_StateMachine.pushUndo = false;
                    m_StateMachine.hideFlags = HideFlags.HideAndDontSave;

                    if (mask != null)
                    {
                        AnimatorControllerLayer[] layers = m_Controller.layers;
                        layers[0].avatarMask = mask;
                        m_Controller.layers = layers;
                    }
                    wasInitialized = false;
                }

                if (m_State == null)
                {
                    m_State = m_StateMachine.AddState("preview");
                    m_State.pushUndo = false;
                    AnimatorControllerLayer[] layers = m_Controller.layers;
                    m_State.motion = m_Clips[0];
                    m_Controller.layers = layers;

                    m_State.iKOnFeet = m_AvatarPreview.IKOnFeet;
                    m_State.hideFlags = HideFlags.HideAndDontSave;
                    wasInitialized = false;
                }


                AnimatorController.SetAnimatorController(m_AvatarPreview.Animator, m_Controller);
                if (AnimatorController.GetEffectiveAnimatorController(m_AvatarPreview.Animator) != m_Controller)
                {
                    AnimatorController.SetAnimatorController(m_AvatarPreview.Animator, m_Controller);
                }
                if (!wasInitialized)
                {
                    m_AvatarPreview.Animator.Play(0, 0, 0);
                    m_AvatarPreview.Animator.Update(0);

                    if (m_FirstInitialization)
                    {
                        m_AvatarPreview.ResetPreviewFocus();
                        m_FirstInitialization = false;
                    }
                }
            }
        }

        internal override bool IsEnabled()
        {
            if (FileUtil.IsReadOnly(m_Clips[0]))
                return false;
            return base.IsEnabled();
        }

        internal override void OnHeaderIconGUI(Rect iconRect)
        {
            // It doesn't make sense to try and use the preview
            Texture2D icon = null;
            bool isLoadingAssetPreview = AssetPreview.IsLoadingAssetPreview(target.GetInstanceID());
            icon = AssetPreview.GetAssetPreview(target);
            if (!icon)
            {
                // We have a static preview it just hasn't been loaded yet. Repaint until we have it loaded.
                if (isLoadingAssetPreview)
                    Repaint();
                icon = AssetPreview.GetMiniThumbnail(target);
            }

            GUI.DrawTexture(iconRect, icon);
        }

        internal override void OnHeaderTitleGUI(Rect titleRect, string header)
        {
            if (m_ClipInfo != null)
                m_ClipInfo.name = EditorGUI.DelayedTextField(titleRect, m_ClipInfo.name, EditorStyles.textField);
            else
                base.OnHeaderTitleGUI(titleRect, header);
        }

        internal override void OnHeaderControlsGUI()
        {
            if (m_ClipInfo != null && takeNames != null && takeNames.Length > 1)
            {
                EditorGUIUtility.labelWidth = 80;
                takeIndex = EditorGUILayout.Popup("Source Take", takeIndex, takeNames);
            }
            else
            {
                base.OnHeaderControlsGUI();

                ModelImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(target)) as ModelImporter;
                if (importer != null && m_ClipInfo == null)
                {
                    if (GUILayout.Button("Edit...", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                    {
                        EditWithImporter(target as AnimationClip);
                    }
                }
            }
        }

        private void DestroyController()
        {
            if (m_AvatarPreview != null && m_AvatarPreview.Animator != null)
            {
                AnimatorController.SetAnimatorController(m_AvatarPreview.Animator, null);
            }

            Object.DestroyImmediate(m_Controller);
            Object.DestroyImmediate(m_State);
            m_Controller = null;
            m_StateMachine = null;
            m_State = null;
        }

        private void SetPreviewAvatar()
        {
            DestroyController();
            InitController();
        }

        void InitPreview()
        {
            if (m_AvatarPreview == null)
            {
                m_AvatarPreview = new AvatarPreview(null, target as Motion);
                m_AvatarPreview.OnAvatarChangeFunc = SetPreviewAvatar;
                m_AvatarPreview.fps = Mathf.RoundToInt((target as AnimationClip).frameRate);
                m_AvatarPreview.ShowIKOnFeetButton = (target as Motion).isHumanMotion;
                m_AvatarPreview.ResetPreviewFocus();
            }

            // force an update on timeControl if AvatarPreviewer is closed when creating/editing animation curves
            // prevent from having a nomralizedTime == -inf
            if (m_AvatarPreview.timeControl.currentTime == Mathf.NegativeInfinity)
                m_AvatarPreview.timeControl.Update();
        }

        public void InitClipTime()
        {
            //case 790259: The length of the clip will be changed by this inspector, so we can't use it for sampling
            m_InitialClipLength = m_Clips[0].stopTime - m_Clips[0].startTime;

            if (m_TimeArea == null)
            {
                m_TimeArea = new TimeArea(true);
                m_TimeArea.hRangeLocked = false;
                m_TimeArea.vRangeLocked = true;
                m_TimeArea.hSlider = true;
                m_TimeArea.vSlider = false;
                m_TimeArea.hRangeMin = m_Clips[0].startTime;
                m_TimeArea.hRangeMax = m_Clips[0].stopTime;
                m_TimeArea.margin = 10;
                m_TimeArea.scaleWithWindow = true;
                m_TimeArea.minWidth = 1.0f / m_Clips[0].frameRate;
                m_TimeArea.SetShownHRangeInsideMargins(m_Clips[0].startTime, m_Clips[0].stopTime);
                m_TimeArea.hTicks.SetTickModulosForFrameRate(m_Clips[0].frameRate);
                m_TimeArea.ignoreScrollWheelUntilClicked = true;
            }

            if (m_EventTimeArea == null)
            {
                m_EventTimeArea = new TimeArea(true);
                m_EventTimeArea.hRangeLocked = true;
                m_EventTimeArea.vRangeLocked = true;
                m_EventTimeArea.hSlider = false;
                m_EventTimeArea.vSlider = false;
                m_EventTimeArea.hRangeMin = 0;
                m_EventTimeArea.hRangeMax = s_EventTimelineMax;
                m_EventTimeArea.margin = 10;
                m_EventTimeArea.scaleWithWindow = true;
                m_EventTimeArea.SetShownHRangeInsideMargins(0, s_EventTimelineMax);
                m_EventTimeArea.hTicks.SetTickModulosForFrameRate(60);
                m_EventTimeArea.ignoreScrollWheelUntilClicked = true;
            }

            if (m_EventManipulationHandler == null)
                m_EventManipulationHandler = new EventManipulationHandler(m_EventTimeArea);
        }

        internal void OnEnable()
        {
            m_Clips = new AnimationClip[targets.Length];
            for (var i = 0; i < targets.Length; ++i)
                m_Clips[i] = targets[i] as AnimationClip;

            m_IsSelectingMultipleClips = m_Clips.Length > 1;
            InitSerializedProperties();

            Undo.undoRedoEvent += OnUndoRedoPerformed;
        }

        void InitSerializedProperties()
        {
            m_WrapModeProperty = serializedObject.FindProperty("m_WrapMode");
            m_ClipSettingsProperty = serializedObject.FindProperty("m_AnimationClipSettings");
            m_LoopTimeProperty = m_ClipSettingsProperty.FindPropertyRelative("m_LoopTime");
            m_LoopBlendProperty = m_ClipSettingsProperty.FindPropertyRelative("m_LoopBlend");
            m_CycleOffsetProperty = m_ClipSettingsProperty.FindPropertyRelative("m_CycleOffset");
            m_LoopBlendOrientationProperty = m_ClipSettingsProperty.FindPropertyRelative("m_LoopBlendOrientation");
            m_KeepOriginalOrientationProperty = m_ClipSettingsProperty.FindPropertyRelative("m_KeepOriginalOrientation");
            m_OrientationOffsetYProperty = m_ClipSettingsProperty.FindPropertyRelative("m_OrientationOffsetY");
            m_LoopBlendPositionYProperty = m_ClipSettingsProperty.FindPropertyRelative("m_LoopBlendPositionY");
            m_KeepOriginalPositionYProperty = m_ClipSettingsProperty.FindPropertyRelative("m_KeepOriginalPositionY");
            m_HeightFromFeetProperty = m_ClipSettingsProperty.FindPropertyRelative("m_HeightFromFeet");
            m_LevelProperty = m_ClipSettingsProperty.FindPropertyRelative("m_Level");
            m_LoopBlendPositionXZProperty = m_ClipSettingsProperty.FindPropertyRelative("m_LoopBlendPositionXZ");
            m_KeepOriginalPositionXZProperty = m_ClipSettingsProperty.FindPropertyRelative("m_KeepOriginalPositionXZ");
            m_MirrorProperty = m_ClipSettingsProperty.FindPropertyRelative("m_Mirror");
        }

        void OnDisable()
        {
            DestroyController();
            if (m_AvatarPreview != null)
            {
                m_AvatarPreview.OnDisable();
                m_AvatarPreview = null;
            }

            Undo.undoRedoEvent -= OnUndoRedoPerformed;
        }

        void OnUndoRedoPerformed(in UndoRedoInfo info)
        {
            if (!m_IsSelectingMultipleClips)
                return;

            for (var i = 0; i < m_Clips.Length; ++i)
                AnimationUtility.RebuildMecanimData(m_Clips[i]);
        }

        public override bool HasPreviewGUI()
        {
            if (m_IsSelectingMultipleClips)
                return false;

            InitPreview();
            return m_AvatarPreview != null;
        }

        public override void OnPreviewSettings()
        {
            m_AvatarPreview.DoPreviewSettings();
        }

        void CalculateQualityCurves()
        {
            for (int i = 0; i < 4; i++)
                m_QualityCurves[i] = new Vector2[2][];

            for (int q = 0; q < 2; q++)
            {
                // [case 491172]
                // There is no need to sample the quality curve outside of the animation range [m_Clip.startTime, m_Clip.stopTime] because the Time area show only the animation range anyway
                // so it not possible for the user to see curve outside of this range.
                float clipStartTime = Mathf.Clamp(m_ClipInfo.firstFrame / m_Clips[0].frameRate, m_Clips[0].startTime, m_Clips[0].stopTime);
                float clipStopTime = Mathf.Clamp(m_ClipInfo.lastFrame / m_Clips[0].frameRate, m_Clips[0].startTime, m_Clips[0].stopTime);

                float fixedTime = (q == 0 ? clipStopTime : clipStartTime);
                float startTime = (q == 0 ? 0 : clipStartTime);
                float stopTime = (q == 0 ? clipStopTime : m_Clips[0].stopTime);
                // Start sample may be a bit before start time; stop sample may be a bit after stop time
                int startSample = Mathf.FloorToInt(startTime * kSamplesPerSecond);
                int stopSample = Mathf.CeilToInt(stopTime * kSamplesPerSecond);

                m_QualityCurves[kPose][q] = new Vector2[stopSample - startSample + 1];
                m_QualityCurves[kRotation][q] = new Vector2[stopSample - startSample + 1];
                m_QualityCurves[kHeight][q] = new Vector2[stopSample - startSample + 1];
                m_QualityCurves[kPosition][q] = new Vector2[stopSample - startSample + 1];

                QualityCurvesTime qualityCurvesTime = new QualityCurvesTime();
                qualityCurvesTime.fixedTime = fixedTime;
                qualityCurvesTime.variableEndStart = startTime;
                qualityCurvesTime.variableEndEnd = stopTime;
                qualityCurvesTime.q = q;

                MuscleClipUtility.CalculateQualityCurves(m_Clips[0], qualityCurvesTime,
                    m_QualityCurves[kPose][q], m_QualityCurves[kRotation][q],
                    m_QualityCurves[kHeight][q], m_QualityCurves[kPosition][q]);
            }
            m_DirtyQualityCurves = false;
        }

        public override void OnInteractivePreviewGUI(Rect r, GUIStyle background)
        {
            bool isRepaint = (Event.current.type == EventType.Repaint);

            InitController();

            if (isRepaint)
                m_AvatarPreview.timeControl.Update();

            // Set to full take range
            AnimationClip clip = target as AnimationClip;
            AnimationClipSettings previewInfo = AnimationUtility.GetAnimationClipSettings(clip);

            // Set settings
            m_AvatarPreview.timeControl.loop = true; // always looping, waiting for UI ctrl...

            // Sample Animation
            if (isRepaint && m_AvatarPreview.PreviewObject != null)
            {
                if (clip.legacy == false && m_AvatarPreview.Animator != null)
                {
                    if (m_State != null)
                        m_State.iKOnFeet = m_AvatarPreview.IKOnFeet;

                    float normalizedTime = previewInfo.stopTime - previewInfo.startTime != 0 ? (m_AvatarPreview.timeControl.currentTime - previewInfo.startTime) / (previewInfo.stopTime - previewInfo.startTime) : 0.0f;
                    m_AvatarPreview.Animator.Play(0, 0, normalizedTime);
                    m_AvatarPreview.Animator.Update(m_AvatarPreview.timeControl.deltaTime);
                }
                else
                {
                    clip.SampleAnimation(m_AvatarPreview.PreviewObject, m_AvatarPreview.timeControl.currentTime);
                }
            }

            m_AvatarPreview.DoAvatarPreview(r, background);
        }

        public void ClipRangeGUI(ref float startFrame, ref float stopFrame, out bool changedStart, out bool changedStop, bool showAdditivePoseFrame, ref float additivePoseframe, out bool changedAdditivePoseframe)
        {
            changedStart = false;
            changedStop = false;
            changedAdditivePoseframe = false;

            m_DraggingRangeBegin = false;
            m_DraggingRangeEnd = false;

            bool invalidRange = (
                startFrame + 0.01f < m_Clips[0].startTime * m_Clips[0].frameRate ||
                startFrame - 0.01f > m_Clips[0].stopTime * m_Clips[0].frameRate ||
                stopFrame + 0.01f < m_Clips[0].startTime * m_Clips[0].frameRate ||
                stopFrame - 0.01f > m_Clips[0].stopTime * m_Clips[0].frameRate);
            bool fixRange = false;
            if (invalidRange)
            {
                GUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label("The clip range is outside of the range of the source take.", EditorStyles.wordWrappedMiniLabel);
                GUILayout.FlexibleSpace();
                GUILayout.BeginVertical();
                GUILayout.Space(5);
                if (GUILayout.Button("Clamp Range"))
                    fixRange = true;
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            // Time line
            Rect timeRect = GUILayoutUtility.GetRect(10, 18 + 15);
            GUI.Label(timeRect, "", "TE Toolbar");
            if (Event.current.type == EventType.Repaint)
                m_TimeArea.rect = timeRect;
            m_TimeArea.BeginViewGUI();
            m_TimeArea.EndViewGUI();
            timeRect.height -= 15;

            // Start and stop markers
            int startHandleId = GUIUtility.GetControlID(3126789, FocusType.Passive);
            int stopHandleId = GUIUtility.GetControlID(3126789, FocusType.Passive);
            int additiveHandleId = GUIUtility.GetControlID(3126789, FocusType.Passive);
            GUI.BeginGroup(new Rect(timeRect.x + 1, timeRect.y + 1, timeRect.width - 2, timeRect.height - 2));
            {
                timeRect.x = timeRect.y = -1;

                // Draw selected range as blue tint
                float startPixel = m_TimeArea.FrameToPixel(startFrame, m_Clips[0].frameRate, timeRect);
                float stopPixel = m_TimeArea.FrameToPixel(stopFrame, m_Clips[0].frameRate, timeRect);
                GUI.Label(new Rect(startPixel, timeRect.y, stopPixel - startPixel, timeRect.height), "", EditorStyles.selectionRect);

                // Draw time ruler
                m_TimeArea.TimeRuler(timeRect, m_Clips[0].frameRate);
                // Current time indicator
                TimeArea.DrawPlayhead(m_TimeArea.TimeToPixel(m_AvatarPreview.timeControl.currentTime, timeRect), timeRect.yMin, timeRect.yMax, 2f, 1f);

                using (new EditorGUI.DisabledScope(invalidRange))
                {
                    // Range handles
                    float startTime = startFrame / m_Clips[0].frameRate;
                    TimeArea.TimeRulerDragMode inPoint = m_TimeArea.BrowseRuler(timeRect, startHandleId, ref startTime, 0, false, "TL InPoint");
                    if (inPoint == TimeArea.TimeRulerDragMode.Cancel)
                    {
                        startFrame = m_DraggingStartFrame;
                    }
                    else if (inPoint != TimeArea.TimeRulerDragMode.None)
                    {
                        startFrame = startTime * m_Clips[0].frameRate;
                        // Snapping bias. Snap to whole frames when zoomed out.
                        startFrame = MathUtils.RoundBasedOnMinimumDifference(startFrame, m_TimeArea.PixelDeltaToTime(timeRect) * m_Clips[0].frameRate * 10);
                        changedStart = true;
                    }
                    float stopTime = stopFrame / m_Clips[0].frameRate;

                    TimeArea.TimeRulerDragMode outPoint = m_TimeArea.BrowseRuler(timeRect, stopHandleId, ref stopTime, 0, false, "TL OutPoint");
                    if (outPoint == TimeArea.TimeRulerDragMode.Cancel)
                    {
                        stopFrame = m_DraggingStopFrame;
                    }
                    else if (outPoint != TimeArea.TimeRulerDragMode.None)
                    {
                        stopFrame = stopTime * m_Clips[0].frameRate;
                        // Snapping bias. Snap to whole frames when zoomed out.
                        stopFrame = MathUtils.RoundBasedOnMinimumDifference(stopFrame, m_TimeArea.PixelDeltaToTime(timeRect) * m_Clips[0].frameRate * 10);
                        changedStop = true;
                    }

                    // Additive pose frame Handle
                    if (showAdditivePoseFrame)
                    {
                        float additivePoseTime = additivePoseframe / m_Clips[0].frameRate;
                        TimeArea.TimeRulerDragMode additivePoint = m_TimeArea.BrowseRuler(timeRect, additiveHandleId, ref additivePoseTime, 0, false, "TL playhead");
                        if (additivePoint == TimeArea.TimeRulerDragMode.Cancel)
                        {
                            additivePoseframe = m_DraggingAdditivePoseFrame;
                        }
                        else if (additivePoint != TimeArea.TimeRulerDragMode.None)
                        {
                            additivePoseframe = additivePoseTime * m_Clips[0].frameRate;
                            // Snapping bias. Snap to whole frames when zoomed out.
                            additivePoseframe = MathUtils.RoundBasedOnMinimumDifference(additivePoseframe, m_TimeArea.PixelDeltaToTime(timeRect) * m_Clips[0].frameRate * 10);
                            changedAdditivePoseframe = true;
                        }
                    }
                }

                if (EditorGUIUtility.hotControl == startHandleId)
                    changedStart = true;
                if (EditorGUIUtility.hotControl == stopHandleId)
                    changedStop = true;
                if (EditorGUIUtility.hotControl == additiveHandleId)
                    changedAdditivePoseframe = true;
            }
            GUI.EndGroup();

            // Start and stop time float fields
            using (new EditorGUI.DisabledScope(invalidRange))
            {
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUI.BeginChangeCheck();
                    startFrame = EditorGUILayout.FloatField(Styles.StartFrame, Mathf.Round(startFrame * 1000) / 1000);
                    if (EditorGUI.EndChangeCheck())
                        changedStart = true;

                    GUILayout.FlexibleSpace();

                    EditorGUI.BeginChangeCheck();
                    stopFrame = EditorGUILayout.FloatField(Styles.EndFrame, Mathf.Round(stopFrame * 1000) / 1000);
                    if (EditorGUI.EndChangeCheck())
                        changedStop = true;
                }
                EditorGUILayout.EndHorizontal();
            }

            changedStart |= fixRange;
            changedStop |= fixRange;

            // Start and stop time value clamping
            if (changedStart)
                startFrame = Mathf.Clamp(startFrame, m_Clips[0].startTime * m_Clips[0].frameRate, Mathf.Clamp(stopFrame, m_Clips[0].startTime * m_Clips[0].frameRate, stopFrame));

            if (changedStop)
                stopFrame = Mathf.Clamp(stopFrame, startFrame, m_Clips[0].stopTime * m_Clips[0].frameRate);

            if (changedAdditivePoseframe)
                additivePoseframe = Mathf.Clamp(additivePoseframe, m_Clips[0].startTime * m_Clips[0].frameRate, m_Clips[0].stopTime * m_Clips[0].frameRate);

            // Keep track of whether we're currently dragging the range or not
            if (changedStart || changedStop || changedAdditivePoseframe)
            {
                if (!m_DraggingRange)
                    m_DraggingRangeBegin = true;
                m_DraggingRange = true;
            }
            else if (m_DraggingRange && EditorGUIUtility.hotControl == 0 && Event.current.type == EventType.Repaint)
            {
                m_DraggingRangeEnd = true;
                m_DraggingRange = false;
                m_DirtyQualityCurves = true;
                Repaint();
            }

            GUILayout.Space(10);
        }

        string GetStatsText()
        {
            string statsText = "";

            bool IsHumanClip = targets.Length == 1 ? (target as Motion).isHumanMotion : false;

            // Muscle clip info is currently only available for humanoid
            if (IsHumanClip)
            {
                statsText = string.Format(Styles.AverageVelocity, m_Clips[0].averageSpeed.ToString("0.000"), (m_Clips[0].averageAngularSpeed * 180.0f / Mathf.PI).ToString("0.0", CultureInfo.InvariantCulture.NumberFormat));
            }

            // Only show stats in final clip not for the preview clip
            if (m_ClipInfo == null)
            {
                AnimationClipStats stats = new AnimationClipStats();
                stats.Reset();
                for (int clipIter = 0; clipIter < targets.Length; clipIter++)
                {
                    AnimationClip clip = targets[clipIter] as AnimationClip;
                    if (clip != null)
                    {
                        AnimationClipStats clipStats = AnimationUtility.GetAnimationClipStats(clip);
                        stats.Combine(clipStats);
                    }
                }

                if (statsText.Length != 0)
                    statsText += '\n';

                float constantRatio = (float)stats.constantCurves / (float)stats.totalCurves * 100.0f;
                float denseRatio = (float)stats.denseCurves / (float)stats.totalCurves * 100.0f;
                float streamRatio = (float)stats.streamCurves / (float)stats.totalCurves * 100.0f;


                statsText += string.Format("Curves Pos: {0} Quaternion: {1} Euler: {2} Scale: {3} Muscles: {4} Generic: {5} PPtr: {6}\n", stats.positionCurves, stats.quaternionCurves, stats.eulerCurves, stats.scaleCurves, stats.muscleCurves, stats.genericCurves, stats.pptrCurves);
                statsText += string.Format("Curves Total: {0}, Constant: {1} ({2}%) Dense: {3} ({4}%) Stream: {5} ({6}%)\n", stats.totalCurves, stats.constantCurves, constantRatio.ToString("0.0", CultureInfo.InvariantCulture.NumberFormat), stats.denseCurves, denseRatio.ToString("0.0", CultureInfo.InvariantCulture.NumberFormat), stats.streamCurves, streamRatio.ToString("0.0", CultureInfo.InvariantCulture.NumberFormat));

                statsText += EditorUtility.FormatBytes(stats.size);
            }

            return statsText;
        }

        private float GetClipLength(int clipIndex)
        {
            if (m_ClipInfo == null)
                return m_Clips[clipIndex].length;
            else
                return (m_ClipInfo.lastFrame - m_ClipInfo.firstFrame) / m_Clips[clipIndex].frameRate;
        }

        // A minimal list of settings to be shown in the Asset Store preview inspector
        internal override void OnAssetStoreInspectorGUI()
        {
            OnInspectorGUI();
        }

        public override void OnInspectorGUI()
        {
            EditorGUIUtility.labelWidth = 50;
            EditorGUIUtility.fieldWidth = 30;

            EditorGUILayout.BeginHorizontal();
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    var hasSameClipLength = true;
                    var hasSameFrameRate = true;
                    var firstClipLength = GetClipLength(0);
                    var firstClipFrameRate = m_Clips[0].frameRate;
                    for (var i = 1; i < m_Clips.Length; ++i)
                    {
                        if (Mathf.Abs(GetClipLength(i) - firstClipLength) > Mathf.Epsilon)
                            hasSameClipLength = false;
                        if (Mathf.Abs(m_Clips[i].frameRate - firstClipFrameRate) > Mathf.Epsilon)
                            hasSameFrameRate = false;
                    }

                    GUILayout.Label(Styles.Length, EditorStyles.label, GUILayout.Width(50 - 4));
                    var clipLengthStr = hasSameClipLength ? firstClipLength.ToString("0.000", CultureInfo.InvariantCulture.NumberFormat) : "-";
                    GUILayout.Label(clipLengthStr, EditorStyles.label);

                    GUILayout.FlexibleSpace();

                    var clipFrameRateStr = hasSameFrameRate ? firstClipFrameRate.ToString() : "-";
                    GUILayout.Label(clipFrameRateStr + " FPS", EditorStyles.label);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (m_IsSelectingMultipleClips)
                MultiClipInspectorGUI();
            else
                SingleClipInspectorGUI();
        }

        private void MultiClipInspectorGUI()
        {
            var sameTypeOfClip = true;
            for (var i = 1; i < m_Clips.Length; ++i)
            {
                if (m_Clips[i].legacy != m_Clips[0].legacy)
                {
                    sameTypeOfClip = false;
                    break;
                }
            }

            if (!sameTypeOfClip)
            {
                EditorGUILayout.HelpBox(Styles.InvalidMultiSelection, MessageType.Error);
                return;
            }

            if (m_Clips[0].legacy)
                MultiLegacyClipGUI();
            else
                MultiMuscleClipGUI();
        }

        private void MultiLegacyClipGUI()
        {
            serializedObject.Update();

            EditorGUIUtility.labelWidth = 0;
            EditorGUIUtility.fieldWidth = 0;

            EditorGUI.BeginChangeCheck();

            var wrapMode = serializedObject.FindProperty("m_WrapMode");
            EditorGUILayout.PropertyField(wrapMode, Styles.WrapMode);

            serializedObject.ApplyModifiedProperties();
        }

        private void MultiMuscleClipGUI()
        {
            serializedObject.Update();

            EditorGUIUtility.labelWidth = 0;
            EditorGUIUtility.fieldWidth = 0;

            var toggleLoopTimeRect = EditorGUILayout.GetControlRect();
            EditorGUI.PropertyField(toggleLoopTimeRect, m_LoopTimeProperty, Styles.LoopTime);

            using (new EditorGUI.DisabledScope(!m_LoopTimeProperty.boolValue))
            {
                EditorGUI.indentLevel++;

                var toggleLoopPoseRect = EditorGUILayout.GetControlRect();
                EditorGUI.PropertyField(toggleLoopPoseRect, m_LoopBlendProperty, Styles.LoopPose);
                EditorGUILayout.PropertyField(m_CycleOffsetProperty, Styles.LoopCycleOffset);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            var areAllClipsEitherHumanOrNonHuman = true;
            var isSelectionHumanClip = true;
            var doesSelectionHaveRootCurves = true;
            var doesSelectionHaveMotionCurves = false;
            for (var i = 0; i < m_Clips.Length; ++i)
            {
                if (m_Clips[0].isHumanMotion != m_Clips[i])
                    areAllClipsEitherHumanOrNonHuman = false;
                if (!m_Clips[i].isHumanMotion)
                    isSelectionHumanClip = false;
                if (!m_Clips[i].hasRootCurves)
                    doesSelectionHaveRootCurves = false;
                if (m_Clips[i].hasMotionCurves)
                    doesSelectionHaveMotionCurves = true;
            }

            if (doesSelectionHaveRootCurves && !doesSelectionHaveMotionCurves)
            {
                // Rotation
                GUILayout.Label(Styles.RootTransformRotation, EditorStyles.label);
                EditorGUI.indentLevel++;

                // Toggle
                var toggleRotRect = EditorGUILayout.GetControlRect();
                EditorGUI.PropertyField(toggleRotRect, m_LoopBlendOrientationProperty, Styles.BakeIntoPoseOrientation);

                // Reference
                BoolPropertyPopup(m_KeepOriginalOrientationProperty,
                    m_LoopBlendOrientationProperty.boolValue ? Styles.BasedUponOrientation : Styles.BasedUponStartOrientation,
                    m_KeepOriginalOrientationProperty.boolValue ? 0 : 1,
                    isSelectionHumanClip ? Styles.BasedUponRotationHumanOpt : Styles.BasedUponRotationOpt);

                // Offset
                EditorGUILayout.PropertyField(m_OrientationOffsetYProperty, Styles.OrientationOffsetY);
                EditorGUI.indentLevel--;

                EditorGUILayout.Space();

                // Position Y
                GUILayout.Label(Styles.RootTransformRotationY, EditorStyles.label);
                EditorGUI.indentLevel++;

                // Toggle
                var toggleYRect = EditorGUILayout.GetControlRect();
                EditorGUI.PropertyField(toggleYRect, m_LoopBlendPositionYProperty, Styles.BakeIntoPosePositionY);

                // Reference
                if (isSelectionHumanClip && areAllClipsEitherHumanOrNonHuman)
                {
                    int offsetHeight;
                    if (m_KeepOriginalPositionYProperty.boolValue)
                        offsetHeight = 0;
                    else if (m_HeightFromFeetProperty.boolValue)
                        offsetHeight = 2;
                    else
                        offsetHeight = 1;

                    var showMixedValueDefault = EditorGUI.showMixedValue;
                    EditorGUI.showMixedValue = m_KeepOriginalPositionYProperty.hasMultipleDifferentValues || m_HeightFromFeetProperty.hasMultipleDifferentValues;

                    offsetHeight =
                        EditorGUILayout.Popup(
                            m_LoopBlendPositionYProperty.boolValue ? Styles.BasedUponStartPositionY : Styles.BasedUponPositionY,
                            offsetHeight,
                            Styles.BasedUponPositionYHumanOpt);

                    EditorGUI.showMixedValue = showMixedValueDefault;

                    if (offsetHeight == 0)
                    {
                        m_KeepOriginalPositionYProperty.boolValue = true;
                        m_HeightFromFeetProperty.boolValue = false;
                    }
                    else if (offsetHeight == 1)
                    {
                        m_KeepOriginalPositionYProperty.boolValue = false;
                        m_HeightFromFeetProperty.boolValue = false;
                    }
                    else
                    {
                        m_KeepOriginalPositionYProperty.boolValue = false;
                        m_HeightFromFeetProperty.boolValue = true;
                    }
                }
                else if (areAllClipsEitherHumanOrNonHuman)
                {
                    BoolPropertyPopup(m_KeepOriginalPositionYProperty,
                        m_LoopBlendPositionYProperty.boolValue ? Styles.BasedUponStartPositionY : Styles.BasedUponPositionY,
                        m_KeepOriginalPositionYProperty.boolValue ? 0 : 1,
                        Styles.BasedUponPositionYOpt);
                }

                // Offset
                EditorGUILayout.PropertyField(m_LevelProperty, Styles.PositionOffsetY);
                EditorGUI.indentLevel--;

                EditorGUILayout.Space();

                // Position XZ
                GUILayout.Label(Styles.RootTransformPositionXZ, EditorStyles.label);
                EditorGUI.indentLevel++;

                // Toggle
                var toggleXZRect = EditorGUILayout.GetControlRect();
                EditorGUI.PropertyField(toggleXZRect, m_LoopBlendPositionXZProperty, Styles.BakeIntoPosePositionXZ);

                // Reference
                BoolPropertyPopup(m_KeepOriginalPositionXZProperty,
                    m_LoopBlendPositionXZProperty.boolValue ? Styles.BasedUponStartPositionXZ : Styles.BasedUponPositionXZ,
                    m_KeepOriginalPositionXZProperty.boolValue ? 0 : 1,
                    isSelectionHumanClip ? Styles.BasedUponPositionXZHumanOpt : Styles.BasedUponPositionXZOpt);

                EditorGUI.indentLevel--;

                EditorGUILayout.Space();
            }

            if (isSelectionHumanClip)
            {
                EditorGUILayout.PropertyField(m_MirrorProperty, Styles.Mirror);
            }

            if (serializedObject.hasModifiedProperties)
            {
                serializedObject.ApplyModifiedProperties();
                foreach(var clip in m_Clips)
                    AnimationUtility.RebuildMecanimData(clip);
            }
        }

        private static void BoolPropertyPopup(SerializedProperty property, GUIContent label, int selectedIndex, GUIContent[] displayOptions)
        {
            var showMixedValueDefault = EditorGUI.showMixedValue;

            var hasMultipleDifferentValues = property.hasMultipleDifferentValues;
            EditorGUI.showMixedValue = hasMultipleDifferentValues;

            EditorGUI.BeginChangeCheck();
            selectedIndex = EditorGUILayout.Popup(label, hasMultipleDifferentValues ? -1 : selectedIndex, displayOptions);

            if (EditorGUI.EndChangeCheck())
                property.boolValue = (selectedIndex == 0);

            EditorGUI.showMixedValue = showMixedValueDefault;
        }

        private void SingleClipInspectorGUI()
        {
            InitPreview();

            if (!m_Clips[0].legacy)
                MuscleClipGUI();
            else
                AnimationClipGUI();
        }

        private void AnimationClipGUI()
        {
            if (m_ClipInfo != null)
            {
                float startFrame = m_ClipInfo.firstFrame;
                float stopFrame = m_ClipInfo.lastFrame;
                float additivePoseFrame = 0;
                bool changedStart = false;
                bool changedStop = false;
                bool changedAdditivePoseFrame = false;
                ClipRangeGUI(ref startFrame, ref stopFrame, out changedStart, out changedStop, false, ref additivePoseFrame, out changedAdditivePoseFrame);
                if (changedStart)
                    m_ClipInfo.firstFrame = startFrame;
                if (changedStop)
                    m_ClipInfo.lastFrame = stopFrame;

                m_AvatarPreview.timeControl.startTime = startFrame / m_Clips[0].frameRate;
                m_AvatarPreview.timeControl.stopTime = stopFrame / m_Clips[0].frameRate;
            }
            else
            {
                m_AvatarPreview.timeControl.startTime = 0;
                m_AvatarPreview.timeControl.stopTime = m_Clips[0].length;
            }

            EditorGUIUtility.labelWidth = 0;
            EditorGUIUtility.fieldWidth = 0;

            if (m_ClipInfo != null)
                m_ClipInfo.loop = EditorGUILayout.Toggle(Styles.AddLoopFrame, m_ClipInfo.loop);

            EditorGUI.BeginChangeCheck();
            int wrap = m_ClipInfo != null ? m_ClipInfo.wrapMode : (int)m_Clips[0].wrapMode;
            wrap = (int)(WrapModeFixed)EditorGUILayout.EnumPopup(Styles.WrapMode, (WrapModeFixed)wrap);
            if (EditorGUI.EndChangeCheck())
            {
                if (m_ClipInfo != null)
                    m_ClipInfo.wrapMode = wrap;
                else
                    m_Clips[0].wrapMode = (WrapMode)wrap;
            }
        }

        void CurveGUI()
        {
            if (m_ClipInfo == null)
                return;

            float time = m_AvatarPreview.timeControl.normalizedTime;

            for (int i = 0; i < m_ClipInfo.GetCurveCount(); i++)
            {
                GUILayout.Space(5);

                GUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button(GUIContent.none, "OL Minus", GUILayout.Width(17))) m_ClipInfo.RemoveCurve(i);
                    else
                    {
                        GUILayout.BeginVertical(GUILayout.Width(125));

                        string prevName = m_ClipInfo.GetCurveName(i);
                        string newName = EditorGUILayout.DelayedTextField(prevName, EditorStyles.textField);

                        if (prevName != newName)
                            m_ClipInfo.SetCurveName(i, newName);

                        SerializedProperty curveProperty = m_ClipInfo.GetCurveProperty(i);
                        AnimationCurve curve = curveProperty.animationCurveValue;

                        int keyCount = curve.length;
                        bool isKey = false;
                        int keyIndex = keyCount - 1;

                        Keyframe[] keys = curve.keys;
                        for (int keyIter = 0; keyIter < keyCount; keyIter++)
                        {
                            if (Mathf.Abs(keys[keyIter].time - time) < 0.0001f)
                            {
                                isKey = true;
                                keyIndex = keyIter;
                                break;
                            }
                            else if (keys[keyIter].time > time)
                            {
                                keyIndex = keyIter;
                                break;
                            }
                        }

                        GUILayout.BeginHorizontal();

                        if (GUILayout.Button(Styles.PrevKeyContent))
                        {
                            if (keyIndex > 0)
                            {
                                keyIndex--;
                                m_AvatarPreview.timeControl.normalizedTime = keys[keyIndex].time;
                            }
                        }

                        if (GUILayout.Button(Styles.NextKeyContent))
                        {
                            if (isKey && keyIndex < keyCount - 1) keyIndex++;
                            m_AvatarPreview.timeControl.normalizedTime = keys[keyIndex].time;
                        }

                        float val, newVal;
                        using (new EditorGUI.DisabledScope(!isKey))
                        {
                            string orgFormat = EditorGUI.kFloatFieldFormatString;
                            EditorGUI.kFloatFieldFormatString = "n3";

                            try
                            {
                                val = curve.Evaluate(time);
                                newVal = EditorGUILayout.FloatField(val, GUILayout.Width(60));
                            }
                            finally
                            {
                                EditorGUI.kFloatFieldFormatString = orgFormat;
                            }
                        }

                        bool addKey = false;

                        if (val != newVal)
                        {
                            if (isKey) curve.RemoveKey(keyIndex);

                            addKey = true;
                        }

                        using (new EditorGUI.DisabledScope(isKey))
                        {
                            if (GUILayout.Button(Styles.AddKeyframeContent))
                            {
                                addKey = true;
                            }
                        }

                        if (addKey)
                        {
                            Keyframe key = new Keyframe();
                            key.time = time;
                            key.value = newVal;
                            key.inTangent = 0;
                            key.outTangent = 0;
                            curve.AddKey(key);
                            keys = curve.keys;
                            m_ClipInfo.SetCurve(i, curve);
                            UnityEditorInternal.AnimationCurvePreviewCache.ClearCache();
                        }

                        GUILayout.EndHorizontal();

                        GUILayout.EndVertical();

                        EditorGUILayout.CurveField(curveProperty, EditorGUI.kCurveColor, new Rect(), GUIContent.none, GUILayout.Height(40));

                        Rect curveRect = GUILayoutUtility.GetLastRect();

                        keyCount = curve.length;

                        TimeArea.DrawPlayhead(curveRect.x + time * curveRect.width, curveRect.yMin, curveRect.yMax, 1f, 1f);

                        for (int keyIter = 0; keyIter < keyCount; keyIter++)
                        {
                            float keyTime = keys[keyIter].time;

                            Handles.color = Color.white;
                            Handles.DrawLine(new Vector3(curveRect.x + keyTime * curveRect.width, curveRect.y + curveRect.height - 10, 0), new Vector3(curveRect.x + keyTime * curveRect.width, curveRect.y + curveRect.height, 0));
                        }
                    }

                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(GUIContent.none, "OL Plus", GUILayout.Width(17))) m_ClipInfo.AddCurve();
            GUILayout.EndHorizontal();
        }

        private void EventsGUI()
        {
            if (m_ClipInfo == null)
                return;

            var currentTime = Mathf.Clamp01(m_AvatarPreview.timeControl.normalizedTime);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Styles.AddEventContent, GUILayout.Width(25)))
            {
                m_ClipInfo.AddEvent(currentTime);
                m_EventManipulationHandler.SelectEvent(m_ClipInfo.GetEvents(), m_ClipInfo.GetEventCount() - 1, m_ClipInfo);
                needsToGenerateClipInfo = true;
            }

            Rect timeRect = GUILayoutUtility.GetRect(10, 18 + 15);
            timeRect.xMin += 5;
            timeRect.xMax -= 4;
            GUI.Label(timeRect, "", "TE Toolbar");

            if (Event.current.type == EventType.Repaint)
                m_EventTimeArea.rect = timeRect;
            timeRect.height -= 15;
            m_EventTimeArea.TimeRuler(timeRect, 100.0f);


            GUI.BeginGroup(new Rect(timeRect.x + 1, timeRect.y + 1, timeRect.width - 2, timeRect.height - 2));
            {
                Rect localTimeRect = new Rect(-1, -1, timeRect.width, timeRect.height);

                AnimationEvent[] events = m_ClipInfo.GetEvents();

                if (m_EventManipulationHandler.HandleEventManipulation(localTimeRect, ref events, m_ClipInfo, currentTime)) // had changed
                {
                    m_ClipInfo.SetEvents(events);
                }

                // Current time indicator
                TimeArea.DrawPlayhead(m_EventTimeArea.TimeToPixel(currentTime, localTimeRect), localTimeRect.yMin, localTimeRect.yMax, 2f, 1f);
            }


            GUI.EndGroup();

            GUILayout.EndHorizontal();

            m_EventManipulationHandler.Draw(timeRect);
        }

        private void MuscleClipGUI()
        {
            EditorGUI.BeginChangeCheck();

            InitController();

            AnimationClipSettings animationClipSettings = AnimationUtility.GetAnimationClipSettings(m_Clips[0]);

            m_StartFrame = m_DraggingRange ? m_StartFrame : animationClipSettings.startTime * m_Clips[0].frameRate;
            m_StopFrame = m_DraggingRange ? m_StopFrame : animationClipSettings.stopTime * m_Clips[0].frameRate;
            m_AdditivePoseFrame = m_DraggingRange ? m_AdditivePoseFrame : animationClipSettings.additiveReferencePoseTime * m_Clips[0].frameRate;

            float startTime = m_StartFrame / m_Clips[0].frameRate;
            float stopTime = m_StopFrame / m_Clips[0].frameRate;
            float additivePoseTime = m_AdditivePoseFrame / m_Clips[0].frameRate;

            MuscleClipQualityInfo clipQualityInfo = MuscleClipUtility.GetMuscleClipQualityInfo(m_Clips[0], startTime,
                stopTime);

            bool IsHumanClip = (target as Motion).isHumanMotion;
            bool hasMotionCurves = m_Clips[0].hasMotionCurves;
            bool hasRootCurves = m_Clips[0].hasRootCurves;
            bool hasGenericRootTransform = m_Clips[0].hasGenericRootTransform;
            bool hasMotionFloatCurves = m_Clips[0].hasMotionFloatCurves;
            bool hasAnyRootCurves = hasRootCurves || hasMotionCurves;

            bool changedStart = false;
            bool changedStop = false;
            bool changedAdditivePoseFrame = false;

            if (m_ClipInfo != null)
            {
                if (hasAnyRootCurves)
                {
                    if (m_DirtyQualityCurves)
                        CalculateQualityCurves();

                    // Calculate curves AFTER first repaint to be more responsive.
                    if (m_QualityCurves[0] == null && Event.current.type == EventType.Repaint)
                    {
                        m_DirtyQualityCurves = true;
                        Repaint();
                    }
                }

                ClipRangeGUI(ref m_StartFrame, ref m_StopFrame, out changedStart, out changedStop, animationClipSettings.hasAdditiveReferencePose, ref m_AdditivePoseFrame, out changedAdditivePoseFrame);
            }

            // Update range info
            if (!m_DraggingRange)
            {
                animationClipSettings.startTime = startTime;
                animationClipSettings.stopTime = stopTime;
                animationClipSettings.additiveReferencePoseTime = additivePoseTime;
            }

            m_AvatarPreview.timeControl.startTime = startTime;
            m_AvatarPreview.timeControl.stopTime = stopTime;

            // While dragging, only update the preview
            if (changedStart)
                m_AvatarPreview.timeControl.nextCurrentTime = startTime;
            if (changedStop)
                m_AvatarPreview.timeControl.nextCurrentTime = stopTime;
            if (changedAdditivePoseFrame)
                m_AvatarPreview.timeControl.nextCurrentTime = additivePoseTime;

            EditorGUIUtility.labelWidth = 0;
            EditorGUIUtility.fieldWidth = 0;


            // Loop time
            // Toggle
            Rect toggleLoopTimeRect = EditorGUILayout.GetControlRect();
            LoopToggle(toggleLoopTimeRect, Styles.LoopTime, ref animationClipSettings.loopTime);

            Rect toggleLoopPoseRect;
            using (new EditorGUI.DisabledScope(!animationClipSettings.loopTime))
            {
                EditorGUI.indentLevel++;

                // Loop pose
                // Toggle
                toggleLoopPoseRect = EditorGUILayout.GetControlRect();
                LoopToggle(toggleLoopPoseRect, Styles.LoopPose, ref animationClipSettings.loopBlend);

                // Offset
                animationClipSettings.cycleOffset = EditorGUILayout.FloatField(Styles.LoopCycleOffset, animationClipSettings.cycleOffset);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            bool showCurves = IsHumanClip && (changedStart || changedStop);

            if (hasRootCurves && !hasMotionCurves)
            {
                // Rotation
                GUILayout.Label(Styles.RootTransformRotation, EditorStyles.label);
                EditorGUI.indentLevel++;
                // Toggle
                Rect toggleRotRect = EditorGUILayout.GetControlRect();
                LoopToggle(toggleRotRect, Styles.BakeIntoPoseOrientation, ref animationClipSettings.loopBlendOrientation);
                // Reference
                int offsetRotation = (animationClipSettings.keepOriginalOrientation ? 0 : 1);

                offsetRotation =
                    EditorGUILayout.Popup(
                        animationClipSettings.loopBlendOrientation
                        ? Styles.BasedUponOrientation
                        : Styles.BasedUponStartOrientation,
                        offsetRotation, IsHumanClip ? Styles.BasedUponRotationHumanOpt : Styles.BasedUponRotationOpt);

                animationClipSettings.keepOriginalOrientation = (offsetRotation == 0);
                // Offset
                if (showCurves)
                    EditorGUILayout.GetControlRect();
                else
                    animationClipSettings.orientationOffsetY = EditorGUILayout.FloatField(Styles.OrientationOffsetY,
                        animationClipSettings
                            .orientationOffsetY);
                EditorGUI.indentLevel--;

                EditorGUILayout.Space();

                // Position Y
                GUILayout.Label(Styles.RootTransformRotationY, EditorStyles.label);
                EditorGUI.indentLevel++;
                // Toggle
                Rect toggleYRect = EditorGUILayout.GetControlRect();
                LoopToggle(toggleYRect, Styles.BakeIntoPosePositionY, ref animationClipSettings.loopBlendPositionY);
                // Reference
                if (IsHumanClip)
                {
                    int offsetHeight;
                    if (animationClipSettings.keepOriginalPositionY)
                        offsetHeight = 0;
                    else if (animationClipSettings.heightFromFeet)
                        offsetHeight = 2;
                    else
                        offsetHeight = 1;

                    offsetHeight =
                        EditorGUILayout.Popup(
                            animationClipSettings.loopBlendPositionY
                            ? Styles.BasedUponStartPositionY
                            : Styles.BasedUponPositionY,
                            offsetHeight, Styles.BasedUponPositionYHumanOpt);

                    if (offsetHeight == 0)
                    {
                        animationClipSettings.keepOriginalPositionY = true;
                        animationClipSettings.heightFromFeet = false;
                    }
                    else if (offsetHeight == 1)
                    {
                        animationClipSettings.keepOriginalPositionY = false;
                        animationClipSettings.heightFromFeet = false;
                    }
                    else
                    {
                        animationClipSettings.keepOriginalPositionY = false;
                        animationClipSettings.heightFromFeet = true;
                    }
                }
                else
                {
                    int offsetHeight = (animationClipSettings.keepOriginalPositionY ? 0 : 1);
                    offsetHeight =
                        EditorGUILayout.Popup(
                            animationClipSettings.loopBlendPositionY
                            ? Styles.BasedUponStartPositionY
                            : Styles.BasedUponPositionY,
                            offsetHeight, Styles.BasedUponPositionYOpt);
                    animationClipSettings.keepOriginalPositionY = (offsetHeight == 0);
                }
                // Offset
                if (showCurves)
                    EditorGUILayout.GetControlRect();
                else
                    animationClipSettings.level = EditorGUILayout.FloatField(Styles.PositionOffsetY,
                        animationClipSettings.level);
                EditorGUI.indentLevel--;

                EditorGUILayout.Space();

                // Position XZ
                GUILayout.Label(Styles.RootTransformPositionXZ, EditorStyles.label);
                EditorGUI.indentLevel++;
                // Toggle
                Rect toggleXZRect = EditorGUILayout.GetControlRect();
                LoopToggle(toggleXZRect, Styles.BakeIntoPosePositionXZ, ref animationClipSettings.loopBlendPositionXZ);
                // Reference
                int offsetPosition = (animationClipSettings.keepOriginalPositionXZ ? 0 : 1);
                offsetPosition =
                    EditorGUILayout.Popup(
                        animationClipSettings.loopBlendPositionXZ
                        ? Styles.BasedUponStartPositionXZ
                        : Styles.BasedUponPositionXZ,
                        offsetPosition, IsHumanClip ? Styles.BasedUponPositionXZHumanOpt : Styles.BasedUponPositionXZOpt);
                animationClipSettings.keepOriginalPositionXZ = (offsetPosition == 0);
                EditorGUI.indentLevel--;

                EditorGUILayout.Space();

                if (hasAnyRootCurves)
                {
                    // Lamps and toggles drawn later to make them be drawn on top
                    if (IsHumanClip)
                    {
                        LoopQualityLampAndCurve(toggleLoopPoseRect, clipQualityInfo.loop, s_LoopMeterHint, changedStart,
                            changedStop,
                            m_QualityCurves[kPose]);
                    }

                    LoopQualityLampAndCurve(toggleRotRect, clipQualityInfo.loopOrientation, s_LoopOrientationMeterHint,
                        changedStart,
                        changedStop, m_QualityCurves[kRotation]);
                    LoopQualityLampAndCurve(toggleYRect, clipQualityInfo.loopPositionY, s_LoopPositionYMeterHint, changedStart,
                        changedStop, m_QualityCurves[kHeight]);
                    LoopQualityLampAndCurve(toggleXZRect, clipQualityInfo.loopPositionXZ, s_LoopPositionXZMeterHint,
                        changedStart,
                        changedStop, m_QualityCurves[kPosition]);
                }
            }

            if (IsHumanClip)
            {
                if (hasMotionCurves)
                {
                    LoopQualityLampAndCurve(toggleLoopPoseRect, clipQualityInfo.loop, s_LoopMeterHint, changedStart,
                        changedStop,
                        m_QualityCurves[kPose]);
                }
                animationClipSettings.mirror = EditorGUILayout.Toggle(Styles.Mirror, animationClipSettings.mirror);
            }

            if (m_ClipInfo != null)
            {
                animationClipSettings.hasAdditiveReferencePose = EditorGUILayout.Toggle(Styles.HasAdditiveReferencePose, animationClipSettings.hasAdditiveReferencePose);
                using (new EditorGUI.DisabledScope(!animationClipSettings.hasAdditiveReferencePose))
                {
                    EditorGUI.indentLevel++;

                    m_AdditivePoseFrame = EditorGUILayout.FloatField(Styles.AdditiveReferencePoseFrame, m_AdditivePoseFrame);
                    m_AdditivePoseFrame = Mathf.Clamp(m_AdditivePoseFrame, m_Clips[0].startTime * m_Clips[0].frameRate, m_Clips[0].stopTime * m_Clips[0].frameRate);

                    animationClipSettings.additiveReferencePoseTime = m_AdditivePoseFrame / m_Clips[0].frameRate;
                    EditorGUI.indentLevel--;
                }
            }

            if (hasGenericRootTransform && hasMotionFloatCurves)
            {
                EditorGUILayout.HelpBox("Root contains both root motion and transform curves", MessageType.Warning);
            }
            else if (hasMotionFloatCurves)
            {
                EditorGUILayout.HelpBox("Root contains root motion curves", MessageType.Info);
            }
            else if (hasGenericRootTransform)
            {
                EditorGUILayout.HelpBox("Root contains position and rotation curves", MessageType.Info);
            }

            // Stats
            string statsText = GetStatsText();
            if (statsText != "")
                GUILayout.Label(statsText, EditorStyles.helpBox);

            EditorGUILayout.Space();

            bool wasChanged;

            // Additional curves
            if (m_ClipInfo != null)
            {
                // Don't make toggling foldout cause GUI.changed to be true (shouldn't cause undoable action etc.)
                wasChanged = GUI.changed;
                m_ShowCurves = EditorGUILayout.Foldout(m_ShowCurves, Styles.Curves, true);
                GUI.changed = wasChanged;
                if (m_ShowCurves)
                    CurveGUI();
            }

            if(m_ClipInfo != null && m_ClipInfo.hasAdditiveReferencePose && m_ClipInfo.GetCurveCount() > 0 &&
               (m_ClipInfo.additiveReferencePoseFrame < m_ClipInfo.firstFrame || m_ClipInfo.additiveReferencePoseFrame > m_ClipInfo.lastFrame))
            {
                EditorGUILayout.HelpBox("Additional curves will be compared to zero values instead of the source clip's curves. "+
                    "This is because the source clip doesn't include these specific curves. " +
                    "To ensure accurate comparisons, consider using a reference pose frame within the clip's start and end frames.", MessageType.Warning);
            }

            if (m_ClipInfo != null)
            {
                wasChanged = GUI.changed;
                m_ShowEvents = EditorGUILayout.Foldout(m_ShowEvents, Styles.Events, true);
                GUI.changed = wasChanged;
                if (m_ShowEvents)
                    EventsGUI();
            }

            if (m_DraggingRangeBegin)
            {
                m_LoopTime = animationClipSettings.loopTime;
                m_LoopBlend = animationClipSettings.loopBlend;
                m_LoopBlendOrientation = animationClipSettings.loopBlendOrientation;
                m_LoopBlendPositionY = animationClipSettings.loopBlendPositionY;
                m_LoopBlendPositionXZ = animationClipSettings.loopBlendPositionXZ;

                animationClipSettings.loopTime = false;
                animationClipSettings.loopBlend = false;
                animationClipSettings.loopBlendOrientation = false;
                animationClipSettings.loopBlendPositionY = false;
                animationClipSettings.loopBlendPositionXZ = false;

                m_DraggingStartFrame = animationClipSettings.startTime * m_Clips[0].frameRate;
                m_DraggingStopFrame = animationClipSettings.stopTime * m_Clips[0].frameRate;
                m_DraggingAdditivePoseFrame = animationClipSettings.additiveReferencePoseTime * m_Clips[0].frameRate;

                //case 790259: The length of the clip will be changed by this inspector, so we can't use it for sampling
                animationClipSettings.startTime = 0;
                animationClipSettings.stopTime = m_InitialClipLength;

                AnimationUtility.SetAnimationClipSettingsNoDirty(m_Clips[0], animationClipSettings);

                DestroyController();
            }

            if (m_DraggingRangeEnd)
            {
                animationClipSettings.loopTime = m_LoopTime;
                animationClipSettings.loopBlend = m_LoopBlend;
                animationClipSettings.loopBlendOrientation = m_LoopBlendOrientation;
                animationClipSettings.loopBlendPositionY = m_LoopBlendPositionY;
                animationClipSettings.loopBlendPositionXZ = m_LoopBlendPositionXZ;
            }

            if (EditorGUI.EndChangeCheck() || m_DraggingRangeEnd)
            {
                if (!m_DraggingRange)
                {
                    Undo.RegisterCompleteObjectUndo(m_Clips, "Muscle Clip Edit");
                    AnimationUtility.SetAnimationClipSettingsNoDirty(m_Clips[0], animationClipSettings);
                    EditorUtility.SetDirty(m_Clips[0]);
                    DestroyController();
                }
            }
        }

        private void LoopToggle(Rect r, GUIContent content, ref bool val)
        {
            if (!m_DraggingRange)
                val = EditorGUI.Toggle(r, content, val);
            else
            {
                EditorGUI.LabelField(r, content, GUIContent.none);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUI.Toggle(r, " ", false);
                }
            }
        }

        private void LoopQualityLampAndCurve(Rect position, float value, int lightMeterHint, bool changedStart, bool changedStop, Vector2[][] curves)
        {
            if (m_ClipInfo == null)
                return;

            GUIStyle style = new GUIStyle(EditorStyles.miniLabel);
            style.alignment = TextAnchor.MiddleRight;

            Rect labelPosition = position;
            labelPosition.xMax -= 20;
            labelPosition.xMin += EditorGUIUtility.labelWidth;
            GUI.Label(labelPosition, Styles.LoopMatch, style);

            Event evt = Event.current;
            int id = GUIUtility.GetControlID(lightMeterHint, FocusType.Passive, position);
            switch (evt.GetTypeForControl(id))
            {
                case EventType.Repaint:
                {
                    const int lampSize = 22;
                    Rect lampPosition = position;
                    float overflow = (lampSize - lampPosition.height) / 2;
                    lampPosition.y -= overflow;
                    lampPosition.xMax += overflow;
                    lampPosition.height = lampSize;
                    lampPosition.xMin = lampPosition.xMax - lampSize;

                    if (value < .33f)
                        GUI.DrawTexture(lampPosition, Styles.RedLightIcon.image);
                    else if (value < .66f)
                        GUI.DrawTexture(lampPosition, Styles.OrangeLightIcon.image);
                    else
                        GUI.DrawTexture(lampPosition, Styles.GreenLightIcon.image);
                    GUI.DrawTexture(lampPosition, Styles.LightRimIcon.image);
                    break;
                }
            }

            if (changedStart || changedStop)
            {
                Rect r = position;
                r.y += r.height + 1;
                r.height = 18;

                // Draw border for quality curve
                GUI.color = new Color(0, 0, 0, !EditorGUIUtility.isProSkin ? 0.8f : 0.3f);
                GUI.DrawTexture(r, EditorGUIUtility.whiteTexture);

                // Subtract 1 pixel so curve is drawn inside border
                r = new RectOffset(-1, -1, -1, -1).Add(r);

                // Draw background for quality curve
                if (!EditorGUIUtility.isProSkin)
                    GUI.color = new Color(90f / 255f, 90f / 255f, 90f / 255f, 1);
                else
                    GUI.color = new Color(65f / 255f, 65f / 255f, 65f / 255f, 1);
                GUI.DrawTexture(r, EditorGUIUtility.whiteTexture);


                GUI.color = Color.white;

                GUI.BeginGroup(r);
                {
                    // Calculate matrix to apply to points
                    Matrix4x4 matrix = m_TimeArea.drawingToViewMatrix;
                    matrix.m00 = r.width / m_TimeArea.shownArea.width;
                    matrix.m11 = r.height - 1;
                    matrix.m03 = -m_TimeArea.shownArea.x * r.width / m_TimeArea.shownArea.width;
                    matrix.m13 = 0;

                    // Apply matrix and assign color for each sample
                    Vector2[] keys = curves[changedStart ? 0 : 1];
                    Vector3[] points = new Vector3[keys.Length];
                    Color[] colors = new Color[keys.Length];
                    Color curveColorRed = new Color(1.0f, 0.3f, 0.3f);
                    Color curveColorOrange = new Color(1.0f, 0.8f, 0.0f);
                    Color curveColorGreen = new Color(0.0f, 1.0f, 0.0f);
                    for (int i = 0; i < points.Length; i++)
                    {
                        points[i] = keys[i];
                        points[i] = matrix.MultiplyPoint3x4(points[i]);
                        if (1 - keys[i].y < .33f)
                            colors[i] = curveColorRed;
                        else if (1 - keys[i].y < .66f)
                            colors[i] = curveColorOrange;
                        else
                            colors[i] = curveColorGreen;
                    }

                    // Draw curve based on calculated points and colors
                    Handles.DrawAAPolyLine(colors, points);

                    // Draw start and end markers
                    GUI.color = new Color(0.3f, 0.6f, 1.0f);
                    // Draw marker for moving end
                    float timePixel = matrix.MultiplyPoint3x4(new Vector3((changedStart ? m_StartFrame : m_StopFrame) / m_Clips[0].frameRate, 0, 0)).x;
                    GUI.DrawTexture(new Rect(timePixel, 0, 1, r.height), EditorGUIUtility.whiteTexture);
                    // Draw marker for static end
                    timePixel = matrix.MultiplyPoint3x4(new Vector3((changedStart ? m_StopFrame : m_StartFrame) / m_Clips[0].frameRate, 0, 0)).x;
                    GUI.DrawTexture(new Rect(timePixel, 0, 1, r.height), EditorGUIUtility.whiteTexture);
                    GUI.color = Color.white;
                }
                GUI.EndGroup();
            }
        }
    }

    // TODO merge this with AnimationWindow once its re-done for 2D
    internal class EventManipulationHandler
    {
        private Rect[] m_EventRects = new Rect[0];
        private static AnimationEvent[] m_EventsAtMouseDown;
        private static float[] m_EventTimes;
        private int m_HoverEvent = -1;

        private string m_InstantTooltipText = null;
        private Vector2 m_InstantTooltipPoint = Vector2.zero;

        private bool[] m_EventsSelected;
        private AnimationWindowEvent[] m_Events;

        private TimeArea m_Timeline;
        private AnimationEventEditorState m_EventEditorState = new();

        public EventManipulationHandler(TimeArea timeArea)
        {
            m_Timeline = timeArea;
        }

        public void SelectEvent(AnimationEvent[] events, int index, AnimationClipInfoProperties clipInfo)
        {
            m_EventsSelected = new bool[events.Length];
            m_EventsSelected[index] = true;

            EditEvents(clipInfo, m_EventsSelected);
        }

        public bool HandleEventManipulation(Rect rect, ref AnimationEvent[] events, AnimationClipInfoProperties clipInfo, float currentTime)
        {
            Texture eventMarker = EditorGUIUtility.IconContent("Animation.EventMarker").image;

            bool hasChanged = false;

            // Calculate rects
            Rect[] hitRects = new Rect[events.Length];
            Rect[] drawRects = new Rect[events.Length];
            int shared = 1;
            int sharedLeft = 0;
            for (int i = 0; i < events.Length; i++)
            {
                AnimationEvent evt = events[i];

                if (sharedLeft == 0)
                {
                    shared = 1;
                    while (i + shared < events.Length && events[i + shared].time == evt.time)
                        shared++;
                    sharedLeft = shared;
                }
                sharedLeft--;

                // Important to take floor of positions of GUI stuff to get pixel correct alignment of
                // stuff drawn with both GUI and Handles/GL. Otherwise things are off by one pixel half the time.
                float keypos = Mathf.Floor(m_Timeline.TimeToPixel(evt.time, rect));
                int sharedOffset = 0;
                if (shared > 1)
                {
                    float spread = Mathf.Min((shared - 1) * (eventMarker.width - 1), (int)(1.0f / m_Timeline.PixelDeltaToTime(rect) - eventMarker.width * 2));
                    sharedOffset = Mathf.FloorToInt(Mathf.Max(0, spread - (eventMarker.width - 1) * (sharedLeft)));
                }

                // UUM-49717
                // Depending on the resolution and the scale of the display, the icon size could be greater than the visible height of the timeline.
                // We divide it so that it fits.
                float absRectHeight = Mathf.Abs(rect.height);
                int divider = absRectHeight > 0 && absRectHeight < eventMarker.height ? Mathf.CeilToInt(eventMarker.height / absRectHeight) : 1;

                Rect r = new Rect(
                    keypos + sharedOffset - eventMarker.width / (2 * divider),
                    (rect.height - 10) * (float)(sharedLeft - shared + 1) / Mathf.Max(1, shared - 1),
                    eventMarker.width / divider,
                    eventMarker.height / divider);

                hitRects[i] = r;
                drawRects[i] = r;
            }

            // Store rects used for tooltip testing
            m_EventRects = new Rect[hitRects.Length];
            for (int i = 0; i < hitRects.Length; i++)
                m_EventRects[i] = new Rect(hitRects[i].x + rect.x, hitRects[i].y + rect.y, hitRects[i].width, hitRects[i].height);

            // Selection control
            if (m_EventsSelected == null || m_EventsSelected.Length != events.Length || m_EventsSelected.Length == 0)
            {
                m_EventsSelected = new bool[events.Length];
                m_Events = null;
            }

            Vector2 offset = Vector2.zero;
            int clickedIndex;
            float startSelection, endSelection;

            // TODO: GUIStyle.none has hopping margins that need to be fixed
            HighLevelEvent hEvent = EditorGUIExt.MultiSelection(
                rect,
                drawRects,
                new GUIContent(eventMarker),
                hitRects,
                ref m_EventsSelected,
                null,
                out clickedIndex,
                out offset,
                out startSelection,
                out endSelection,
                GUIStyle.none
            );


            if (hEvent != HighLevelEvent.None)
            {
                switch (hEvent)
                {
                    case HighLevelEvent.BeginDrag:
                        m_EventsAtMouseDown = events;
                        m_EventTimes = new float[events.Length];
                        for (int i = 0; i < events.Length; i++)
                            m_EventTimes[i] = events[i].time;
                        break;
                    case HighLevelEvent.SelectionChanged:
                        EditEvents(clipInfo, m_EventsSelected);
                        break;
                    case HighLevelEvent.Delete:
                        hasChanged = DeleteEvents(ref events, m_EventsSelected);
                        break;
                    case HighLevelEvent.Copy:
                        AnimationWindowEventsClipboard.CopyEvents(events, m_EventsSelected);
                        break;
                    case HighLevelEvent.Paste:
                        hasChanged = PasteEvents(ref events, ref m_EventsSelected, currentTime);
                        if (hasChanged)
                            EditEvents(clipInfo, m_EventsSelected);
                        break;
                    case HighLevelEvent.Drag:
                        for (int i = events.Length - 1; i >= 0; i--)
                        {
                            if (m_EventsSelected[i])
                            {
                                AnimationEvent evt = m_EventsAtMouseDown[i];
                                evt.time = Mathf.Clamp01(m_EventTimes[i] + (offset.x / rect.width));
                            }
                        }
                        int[] order = new int[m_EventsSelected.Length];
                        for (int i = 0; i < order.Length; i++)
                        {
                            order[i] = i;
                        }
                        System.Array.Sort(m_EventsAtMouseDown, order, new AnimationEventTimeLine.EventComparer());
                        bool[] selectedOld = (bool[])m_EventsSelected.Clone();
                        float[] timesOld = (float[])m_EventTimes.Clone();
                        for (int i = 0; i < order.Length; i++)
                        {
                            m_EventsSelected[i] = selectedOld[order[i]];
                            m_EventTimes[i] = timesOld[order[i]];
                        }

                        events = m_EventsAtMouseDown;
                        hasChanged = true;
                        break;

                    case HighLevelEvent.ContextClick:
                        CreateContextMenu(clipInfo, events[clickedIndex].time, clickedIndex, m_EventsSelected);
                        // Mouse may move while context menu is open - make sure instant tooltip is handled
                        m_InstantTooltipText = null;
                        break;
                }
            }

            // Bring up menu when context-clicking on an empty timeline area (context-clicking on events is handled above)
            if (Event.current.type == EventType.ContextClick && rect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                float mousePosTime = Mathf.Max(m_Timeline.PixelToTime(Event.current.mousePosition.x, rect), 0.0f);
                CreateContextMenu(clipInfo, mousePosTime, -1, m_EventsSelected);
                // Mouse may move while context menu is open - make sure instant tooltip is handled
                m_InstantTooltipText = null;
            }

            CheckRectsOnMouseMove(rect, events, hitRects);

            return hasChanged;
        }

        void CreateContextMenu(AnimationClipInfoProperties info, float time, int eventIndex, bool[] selectedEvents)
        {
            GenericMenu menu = new GenericMenu();
            var ctx = new EventModificationContextMenuObject(info, time, eventIndex, selectedEvents);
            var selectedCount = selectedEvents.Count(selected => selected);

            menu.AddItem(AnimationClipEditor.Styles.AddEvent, false, EventLineContextMenuAdd, ctx);
            if (selectedCount > 0 || eventIndex != -1)
            {
                menu.AddItem(selectedCount > 1 ? AnimationClipEditor.Styles.DeleteEvents : AnimationClipEditor.Styles.DeleteEvent, false, EventLineContextMenuDelete, ctx);
                menu.AddItem(AnimationClipEditor.Styles.CopyEvents, false, EventLineContextMenuCopy, ctx);
            }
            else
            {
                menu.AddDisabledItem(AnimationClipEditor.Styles.DeleteEvents);
                menu.AddDisabledItem(AnimationClipEditor.Styles.CopyEvents);
            }
            if (AnimationWindowEventsClipboard.CanPaste())
                menu.AddItem(AnimationClipEditor.Styles.PasteEvents, false, EventLineContextMenuPaste, ctx);
            else
                menu.AddDisabledItem(AnimationClipEditor.Styles.PasteEvents);
            menu.ShowAsContext();
        }

        private class EventModificationContextMenuObject
        {
            public AnimationClipInfoProperties m_Info;
            public float m_Time;
            public int m_Index;
            public bool[] m_Selected;

            public EventModificationContextMenuObject(AnimationClipInfoProperties info, float time, int index, bool[] selected)
            {
                m_Info = info;
                m_Time = time;
                m_Index = index;
                m_Selected = selected;
            }
        }

        public void EventLineContextMenuAdd(object obj)
        {
            EventModificationContextMenuObject context = (EventModificationContextMenuObject)obj;

            context.m_Info.AddEvent(context.m_Time);
            context.m_Info.ApplyModifiedProperties();
            SelectEvent(context.m_Info.GetEvents(), context.m_Info.GetEventCount() - 1, context.m_Info);
        }

        public void EventLineContextMenuDelete(object obj)
        {
            EventModificationContextMenuObject context = (EventModificationContextMenuObject)obj;

            if (Array.Exists(context.m_Selected, selected => selected))
            {
                for (int i = context.m_Selected.Length - 1; i >= 0; --i)
                {
                    if (context.m_Selected[i])
                        context.m_Info.RemoveEvent(i);
                }
            }
            else if (context.m_Index >= 0)
            {
                context.m_Info.RemoveEvent(context.m_Index);
            }
            context.m_Info.ApplyModifiedProperties();
        }

        static void EventLineContextMenuCopy(object obj)
        {
            var ctx = (EventModificationContextMenuObject)obj;
            AnimationWindowEventsClipboard.CopyEvents(ctx.m_Info.GetEvents(), ctx.m_Selected, ctx.m_Index);
        }

        void EventLineContextMenuPaste(object obj)
        {
            var ctx = (EventModificationContextMenuObject)obj;
            var events = ctx.m_Info.GetEvents();
            var changed = PasteEvents(ref events, ref ctx.m_Selected, ctx.m_Time);
            if (changed)
            {
                ctx.m_Info.SetEvents(events);
                ctx.m_Info.ApplyModifiedProperties();
                m_EventsSelected = ctx.m_Selected;
                EditEvents(ctx.m_Info, ctx.m_Selected);
            }
        }

        private void CheckRectsOnMouseMove(Rect eventLineRect, AnimationEvent[] events, Rect[] hitRects)
        {
            Vector2 mouse = Event.current.mousePosition;
            bool hasFound = false;
            m_InstantTooltipText = "";

            if (events.Length == hitRects.Length)
            {
                for (int i = hitRects.Length - 1; i >= 0; i--)
                {
                    if (hitRects[i].Contains(mouse))
                    {
                        hasFound = true;
                        if (m_HoverEvent != i)
                        {
                            m_HoverEvent = i;
                            m_InstantTooltipText = events[m_HoverEvent].functionName;
                            m_InstantTooltipPoint = new Vector2(mouse.x, mouse.y);
                        }
                    }
                }
            }
            if (!hasFound)
                m_HoverEvent = -1;
        }

        public void Draw(Rect window)
        {
            EditorGUI.indentLevel++;
            if (m_Events != null && m_Events.Length > 0)
                AnimationWindowEventInspector.OnEditAnimationEvents(m_Events, m_EventEditorState);
            else
                AnimationWindowEventInspector.OnDisabledAnimationEvent();

            EditorGUI.indentLevel--;

            if (!string.IsNullOrEmpty(m_InstantTooltipText))
            {
                // Draw body of tooltip
                GUIStyle style = (GUIStyle)"AnimationEventTooltip";
                Vector2 size = style.CalcSize(new GUIContent(m_InstantTooltipText));
                Rect rect = new Rect(window.x + m_InstantTooltipPoint.x, window.y + m_InstantTooltipPoint.y, size.x, size.y);

                // Right align tooltip rect if it would otherwise exceed the bounds of the window
                if (rect.xMax > window.width)
                    rect.x = window.width - rect.width;

                GUI.Label(rect, m_InstantTooltipText, style);
            }
        }

        public bool DeleteEvents(ref AnimationEvent[] eventList, bool[] deleteIndices)
        {
            bool deletedAny = false;

            for (int i = eventList.Length - 1; i >= 0; i--)
            {
                if (deleteIndices[i])
                {
                    ArrayUtility.RemoveAt(ref eventList, i);
                    deletedAny = true;
                }
            }

            if (deletedAny)
            {
                m_EventsSelected = new bool[eventList.Length];
                m_Events = null;
            }

            return deletedAny;
        }

        static bool PasteEvents(ref AnimationEvent[] eventList, ref bool[] selected, float time)
        {
            var newEvents = AnimationWindowEventsClipboard.AddPastedEvents(eventList, time, out var newSelected);
            if (newEvents == null)
                return false;
            eventList = newEvents;
            selected = newSelected;
            return true;
        }

        public void EditEvents(AnimationClipInfoProperties clipInfo, bool[] selectedIndices)
        {
            List<AnimationWindowEvent> awEvents = new List<AnimationWindowEvent>();

            for (int index = 0; index < selectedIndices.Length; ++index)
            {
                if (selectedIndices[index])
                    awEvents.Add(AnimationWindowEvent.Edit(clipInfo, index));
            }

            m_Events = awEvents.ToArray();
        }

        public void UpdateEvents(AnimationClipInfoProperties clipInfo)
        {
            if (m_Events == null)
                return;

            foreach (AnimationWindowEvent awEvent in m_Events)
            {
                awEvent.clipInfo = clipInfo;
            }
        }
    }
}
