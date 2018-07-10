#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using Vimeo;
using System.Linq;

namespace Vimeo.Recorder
{
    [CustomEditor(typeof(VimeoRecorder))]
    public class VimeoRecorderEditor : BaseEditor
    {
        static bool recordingFold;
        static bool publishFold;
        static bool vimeoFold;
        static bool slackFold;

        [MenuItem("GameObject/Video/Vimeo Recorder")]
        private static void CreateRecorderPrefab() {
            GameObject go = Instantiate(Resources.Load("Prefabs/[VimeoRecorder]") as GameObject);
            go.name = "[VimeoRecorder]";
        }

        void OnDisable()
        {
            EditorPrefs.SetBool("recordingFold", recordingFold);
            EditorPrefs.SetBool("publishFold", publishFold);
            EditorPrefs.SetBool("vimeoFold", vimeoFold);
            EditorPrefs.SetBool("slackFold", slackFold);

            var recorder = target as VimeoRecorder;
            recorder.recordOnStart = false;
        }

        void OnEnable()
        {
            recordingFold = EditorPrefs.GetBool("recordingFold");
            publishFold = EditorPrefs.GetBool("publishFold");
            vimeoFold = EditorPrefs.GetBool("vimeoFold");
            slackFold = EditorPrefs.GetBool("slackFold");
        }

        public override void OnInspectorGUI()
        {
            var recorder = target as VimeoRecorder;
            DrawConfig(recorder);
            EditorUtility.SetDirty(target);
        }

        public void DrawConfig(VimeoRecorder recorder)
        {
            var so = serializedObject;

            // Help Nav            
            GUILayout.BeginHorizontal();
            var style = new GUIStyle();
            style.border = new RectOffset(0,0,0,0);
            GUILayout.Box("", style);
            
            GUIManageVideosButton();
            GUIHelpButton();
            GUISignOutButton();

            GUILayout.EndHorizontal();
            EditorGUILayout.Space();
    
            // Vimeo Settings
            if (recorder.Authenticated() && recorder.vimeoSignIn) {
                
                if (!recorder.isRecording) {
                    DrawRecorderConfig(recorder);
                }

                publishFold = EditorGUILayout.Foldout(publishFold, "Publish to");
                
                if (publishFold) {
                    EditorGUI.indentLevel++;

                    GUILayout.BeginHorizontal();
                    vimeoFold = EditorGUILayout.Foldout(vimeoFold, "Vimeo");
                    GUILayout.EndHorizontal();

                    if (vimeoFold) {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(so.FindProperty("videoName"));
                        EditorGUILayout.PropertyField(so.FindProperty("privacyMode"));

                        GUISelectFolder();

                        EditorGUILayout.PropertyField(so.FindProperty("commentMode"), new GUIContent("Comments"));
                        EditorGUILayout.PropertyField(so.FindProperty("enableDownloads"));
                        // EditorGUILayout.PropertyField(so.FindProperty("enableReviewPage"));

                        if (VimeoApi.PrivacyModeDisplay.OnlyPeopleWithAPassword == recorder.privacyMode) {
                            EditorGUILayout.PropertyField(so.FindProperty("videoPassword"), new GUIContent("Password"));
                        }

                        EditorGUILayout.PropertyField(so.FindProperty("openInBrowser"));
                        EditorGUI.indentLevel--;
                    }

                    DrawSlackConfig(recorder);
                    EditorGUI.indentLevel--;
                }

                DrawRecordingControls();
            }

            DrawVimeoAuth(recorder);

            so.ApplyModifiedProperties();
        }

        public void DrawRecordingControls()
        {
            var recorder = target as VimeoRecorder;
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            if (EditorApplication.isPlaying && recorder.encoderType == EncoderType.MediaEncoder) {
                if (recorder.isRecording) {
                    GUI.backgroundColor = Color.green;

                    if (GUILayout.Button("Finish & Upload", GUILayout.Height(30))) {
                        recorder.EndRecording();
                    }

                    GUI.backgroundColor = Color.red;
                    if (GUILayout.Button("Cancel", GUILayout.Height(30))) {
                        recorder.CancelRecording();
                    }
                }
                else if (recorder.isUploading) {
                    GUI.backgroundColor = Color.red;
                    if (GUILayout.Button("Cancel", GUILayout.Height(30))) {
                        recorder.CancelRecording();
                    }
                }
                else {
                    GUI.backgroundColor = Color.green;
                    if (GUILayout.Button("Start Recording", GUILayout.Height(30))) {
                        recorder.BeginRecording();
                    }
                }

                GUI.backgroundColor = Color.white;
            }
            else {
                if (recorder.encoderType != EncoderType.AVProMovieCapture) {
                    GUI.backgroundColor = Color.green;
                    if (GUILayout.Button("Start Recording", GUILayout.Height(30))) {
                        recorder.recordOnStart = true;
                        EditorApplication.isPlaying = true;
                    }
                    GUI.backgroundColor = Color.white;
                }
            }
            GUILayout.EndHorizontal();

            // Recording progress bar
          if (EditorApplication.isPlaying && recorder.isRecording && recorder.encoderType == EncoderType.MediaEncoder) {
                EditorGUILayout.Space();
                var rect = EditorGUILayout.BeginHorizontal();
                rect.height = 20;
                GUILayout.Box("", GUILayout.Height(20));

                int seconds = recorder.encoder.GetCurrentFrame() / recorder.frameRate;
                float progress = recorder.encoder.GetCurrentFrame() / (float)(recorder.recordDuration * recorder.frameRate);
                
                if (recorder.recordMode != RecordMode.Duration) {
                    progress = 0;
                }

                EditorGUI.ProgressBar(rect, progress, seconds + " seconds (" + recorder.encoder.GetCurrentFrame().ToString() + " frames)");

                GUILayout.EndHorizontal();
            }

            if (EditorApplication.isPlaying && recorder.isUploading) {
                EditorGUILayout.Space();
                var rect = EditorGUILayout.BeginHorizontal();
                rect.height = 20;
                GUILayout.Box("", GUILayout.Height(20));
                EditorGUI.ProgressBar(rect, recorder.uploadProgress, "Uploading to Vimeo...");
                GUILayout.EndHorizontal();
            }
        }

        public void DrawRecorderConfig(VimeoRecorder recorder)
        {
            var so = serializedObject;

            GUILayout.BeginHorizontal();
            recordingFold = EditorGUILayout.Foldout(recordingFold, "Recording");
            GUILayout.EndHorizontal();

            if (recordingFold) {
                EditorGUI.indentLevel++;

#if VIMEO_AVPRO_CAPTURE_SUPPORT
                EditorGUILayout.PropertyField(so.FindProperty("encoderType"), new GUIContent("Encoder"));    

                if (recorder.encoderType == Vimeo.Recorder.EncoderType.AVProMovieCapture) {
                    EditorGUILayout.PropertyField(so.FindProperty("avproEncoder"), new GUIContent("AVPro Object"));
                }
                else {
#endif
                EditorGUILayout.PropertyField(so.FindProperty("defaultVideoInput"), new GUIContent("Input"));

                if (recorder.defaultVideoInput == Vimeo.Recorder.VideoInputType.Camera) {
                    EditorGUILayout.PropertyField(so.FindProperty("defaultCamera"), new GUIContent("Camera"));
                }
#if UNITY_2018_1_OR_NEWER
                else if (recorder.defaultVideoInput == Vimeo.Recorder.VideoInputType.Camera360) {
                    EditorGUILayout.PropertyField(so.FindProperty("defaultCamera360"), new GUIContent("Camera"));
                    EditorGUILayout.PropertyField(so.FindProperty("defaultRenderMode360"), new GUIContent("Render mode"));
                }
#endif

                if (recorder.defaultVideoInput == Vimeo.Recorder.VideoInputType.Camera || 
                    recorder.defaultVideoInput == Vimeo.Recorder.VideoInputType.Screen) {
                    EditorGUILayout.PropertyField(so.FindProperty("defaultResolution"), new GUIContent("Resolution"));
                    
                    if (recorder.defaultResolution != Vimeo.Recorder.Resolution.Window) {
                        EditorGUILayout.PropertyField(so.FindProperty("defaultAspectRatio"), new GUIContent("Aspect ratio"));
                    }
                }

                EditorGUILayout.PropertyField(so.FindProperty("frameRate"));
                EditorGUILayout.PropertyField(so.FindProperty("realTime"));

                if (recorder.realTime) {
#if UNITY_2018_1_OR_NEWER
                    EditorGUILayout.PropertyField(so.FindProperty("recordAudio"));
#else
                    recorder.recordAudio = false;
#endif 
                }
                EditorGUILayout.PropertyField(so.FindProperty("recordMode"));

                if (recorder.recordMode == RecordMode.Duration) {
                    EditorGUILayout.PropertyField(so.FindProperty("recordDuration"), new GUIContent("Duration (sec)"));
                }
#if VIMEO_AVPRO_CAPTURE_SUPPORT
                }
#endif

                EditorGUI.indentLevel--;
            }
        }

        public void DrawSlackAuth(VimeoRecorder recorder)
        {
            GUIStyle customstyle = new GUIStyle();
            customstyle.margin = new RectOffset(40, 0, 0, 0);
            
            GUILayout.BeginHorizontal();

            var so = serializedObject;
            if (!recorder.SlackAuthenticated()) {
                EditorGUILayout.PropertyField(so.FindProperty("slackToken"));
                if (recorder.slackToken == null || recorder.slackToken == "") {
                    if (GUILayout.Button("Get token", GUILayout.Width(75))) {
                        Application.OpenURL("https://authy.vimeo.com/auth/slack");
                    }
                }
                else {                
                    if (GUILayout.Button("Get token", GUILayout.Width(75))) {
                        Application.OpenURL("https://authy.vimeo.com/auth/slack");
                    }

                    GUILayout.EndHorizontal();        
                    GUILayout.BeginHorizontal(customstyle);
                    GUI.backgroundColor = Color.green;

                    if (GUILayout.Button("Sign in")) {
                       recorder.SetSlackToken(recorder.slackToken);
                       recorder.slackToken = null;
                       GUI.FocusControl(null);
                    }
                    GUI.backgroundColor = Color.white;
                }
            } 

            GUILayout.EndHorizontal();
        }

        public void DrawSlackConfig(VimeoRecorder recorder)
        {
            var so = serializedObject;

            GUILayout.BeginHorizontal();
            slackFold = EditorGUILayout.Foldout(slackFold, "Slack");
            if (recorder.SlackAuthenticated() && GUILayout.Button("Sign out", GUILayout.Width(60))) {
                recorder.SetSlackToken(null);   
            }
            GUILayout.EndHorizontal();

            if (slackFold) {
                EditorGUI.indentLevel++;
                if (recorder.SlackAuthenticated()) {
                    EditorGUILayout.PropertyField(so.FindProperty("slackChannel"));
                    EditorGUILayout.PropertyField(so.FindProperty("defaultShareLink"), new GUIContent("Share Link"));
                    EditorGUILayout.PropertyField(so.FindProperty("autoPostToChannel"), new GUIContent("Post to Channel"));
                } 

                DrawSlackAuth(recorder);
                EditorGUI.indentLevel--;
            }
        }
    }
}
#endif