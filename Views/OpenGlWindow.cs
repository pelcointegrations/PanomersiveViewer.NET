using System;
using System.Drawing;
using System.Windows.Forms;
using ImExTkNet;

namespace PanomersiveViewerNET
{
    /// <summary>
    /// The OpenGlWindow is a Windows Form UserControl that is used to display the rendered OpenGL video.
    /// </summary>
    public partial class OpenGlWindow : UserControl
    {
        private IntPtr _hdc;
        private IntPtr _glContext;
        private Graphics _graphics;

        /// <summary>
        /// Initializes a new instance of <see cref="OpenGlWindow"/>.
        /// </summary>
        public OpenGlWindow()
        {
            InitializeComponent();
            SetStyle(ControlStyles.EnableNotifyMessage, true);
            MakeGlCurrent();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int CS_VREDRAW = 0x1;
                const int CS_HREDRAW = 0x2;
                const int CS_OWNDC = 0x20;
                var cp = base.CreateParams;
                cp.ClassStyle = cp.ClassStyle | CS_VREDRAW | CS_HREDRAW | CS_OWNDC;
                cp.Style &= ~0x02000000;
                return cp;
            }
        }

        public IntPtr GetContext()
        {
            return _glContext;
        }

        public void MakeGlCurrent()
        {
            if (_glContext == IntPtr.Zero)
            {
                _graphics = CreateGraphics();
                _hdc = _graphics.GetHdc();
                _glContext = Wgl.CreateOpenGlContext(_hdc);
                if (_glContext == IntPtr.Zero)
                    throw new Exception("Unable to create opengl context");
            }

            Wgl.MakeCurrent(_hdc, _glContext);
        }

        public void Release()
        {
            if (_glContext == IntPtr.Zero)
                return;

            Wgl.DeleteContext(_glContext);
            _glContext = IntPtr.Zero;
        }

        public void SetGlContext(IntPtr openGlContext)
        {
            _glContext = Wgl.CreateOpenGlContext(_hdc, openGlContext);
        }

        public void SwapBuffers()
        {
            Wgl.SwapBuffer(_hdc);
        }

        public void Use()
        {
            Wgl.MakeCurrent(_hdc, _glContext);
        }

        [System.Security.Permissions.PermissionSet(System.Security.Permissions.SecurityAction.Demand, Name = "FullTrust")]
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x14)
                return;

            base.WndProc(ref m);
        }
    }
}
