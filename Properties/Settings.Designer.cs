﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace PanomersiveViewerNET.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "16.7.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public global::PanomersiveViewerNET.Models.CameraCollectionModel CameraCollection {
            get {
                return ((global::PanomersiveViewerNET.Models.CameraCollectionModel)(this["CameraCollection"]));
            }
            set {
                this["CameraCollection"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string SelectedCamera {
            get {
                return ((string)(this["SelectedCamera"]));
            }
            set {
                this["SelectedCamera"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool OverrideCameraTilt {
            get {
                return ((bool)(this["OverrideCameraTilt"]));
            }
            set {
                this["OverrideCameraTilt"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("0")]
        public int CameraTiltAngle {
            get {
                return ((int)(this["CameraTiltAngle"]));
            }
            set {
                this["CameraTiltAngle"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("ViewEdge")]
        public global::ImExTkNet.PtzLimitMode PtzLimitMode {
            get {
                return ((global::ImExTkNet.PtzLimitMode)(this["PtzLimitMode"]));
            }
            set {
                this["PtzLimitMode"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool AutoZoomIn {
            get {
                return ((bool)(this["AutoZoomIn"]));
            }
            set {
                this["AutoZoomIn"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool AutoZoomOut {
            get {
                return ((bool)(this["AutoZoomOut"]));
            }
            set {
                this["AutoZoomOut"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("True")]
        public bool AutoPanTilt {
            get {
                return ((bool)(this["AutoPanTilt"]));
            }
            set {
                this["AutoPanTilt"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Automatic")]
        public global::ImExTkNet.StreamOptimizedTypes StreamOptimizedType {
            get {
                return ((global::ImExTkNet.StreamOptimizedTypes)(this["StreamOptimizedType"]));
            }
            set {
                this["StreamOptimizedType"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Panomersive")]
        public global::PanomersiveViewerNET.Utils.LayoutOptions LayoutType {
            get {
                return ((global::PanomersiveViewerNET.Utils.LayoutOptions)(this["LayoutType"]));
            }
            set {
                this["LayoutType"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool ShowFps {
            get {
                return ((bool)(this["ShowFps"]));
            }
            set {
                this["ShowFps"] = value;
            }
        }
    }
}
