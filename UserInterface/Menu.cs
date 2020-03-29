﻿using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using SystemTrayMenu.DataClasses;
using SystemTrayMenu.Helper;
using SystemTrayMenu.Utilities;
using EventHandler = SystemTrayMenu.Helper.EventHandler;

namespace SystemTrayMenu.UserInterface
{
    internal partial class Menu : Form
    {
        internal new event EventHandler MouseWheel;
        internal event EventHandler Deactivated;
        internal event EventHandler UserClickedOpenFolder;

        internal event Action<Keys> CmdKeyProcessed;

        internal enum Type
        {
            Main,
            Sub,
            Empty,
            NoAccess,
            MaxReached
        }

        internal bool IsFadingIn => FadeForm.IsFadingIn;

        internal bool IsFadingOut => FadeForm.IsFadingOut;

        internal int Level = 0;

        private readonly FadeForm FadeForm = null;
        private bool autoResizeRowsDone = false;

        internal enum MenuType { Default, DisposedFake };

        internal Menu(MenuType menuType = MenuType.Default)
        {
            FadeForm = new FadeForm(this);

            InitializeComponent();
            SetDoubleBuffer(dgv, true);

            DataGridViewCellStyle dgvCellStyle = new DataGridViewCellStyle
            {
                SelectionBackColor = MenuDefines.ColorSelectedItem,
                SelectionForeColor = Color.Black
            };
            dgv.DefaultCellStyle = dgvCellStyle;

            VScrollBar scrollBar = dgv.Controls.OfType<VScrollBar>().First();
            scrollBar.MouseWheel += dgv_MouseWheel;

            if (menuType == MenuType.DisposedFake)
            {
                Dispose();
            }
        }

        private static void SetDoubleBuffer(Control ctl, bool DoubleBuffered)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty,
                null, ctl, new object[] { DoubleBuffered },
                System.Globalization.CultureInfo.InvariantCulture);
        }

        internal void SetTypeSub()
        {
            SetType(Type.Sub);
        }
        internal void SetTypeEmpty()
        {
            SetType(Type.Empty);
        }
        internal void SetTypeNoAccess()
        {
            SetType(Type.NoAccess);
        }

        internal void SetType(Type type)
        {
            switch (type)
            {
                case Type.Sub:
                    if (!labelTitle.IsDisposed)
                    {
                        labelTitle.Dispose();
                    }
                    break;
                case Type.Empty:
                    SetTitle(Language.Translate("Folder empty"));
                    labelTitle.BackColor = MenuDefines.ColorTitleWarning;
                    break;
                case Type.NoAccess:
                    SetTitle(Language.Translate("Folder inaccessible"));
                    labelTitle.BackColor = MenuDefines.ColorTitleWarning;
                    break;
                case Type.MaxReached:
                    SetTitle($"Max {MenuDefines.MenusMax - 1} Menus");
                    labelTitle.BackColor = MenuDefines.ColorTitleWarning;
                    break;
                case Type.Main:
                    break;
                default:
                    break;
            }
        }

        internal bool IsVisible()
        {
            return Visible;
        }

        internal bool IsActive(Form activeForm)
        {
            bool isActive = (this == activeForm);
            return isActive;
        }

        internal bool IsMouseOn(Point mousePosition)
        {
            bool isMouseOn = Visible && Opacity >= MenuDefines.OpacityHalfValue
                && ClientRectangle.Contains(
                  PointToClient(mousePosition));
            return isMouseOn;
        }

        internal DataGridView GetDataGridView()
        {
            return dgv;
        }

        internal void SetTitle(string title)
        {
            if (!string.IsNullOrEmpty(title))
            {
                if (title.Length > MenuDefines.LengthMax)
                {
                    title = $"{title.Substring(0, MenuDefines.LengthMax)}...";
                }
                labelTitle.Text = title;
            }
        }

        internal void FadeIn()
        {
            FadeForm.FadeIn();
        }

        internal void FadeHalf()
        {
            FadeForm.FadeHalf();
        }

        internal void FadeOut()
        {
            FadeForm.FadeOut();
        }

        internal void AdjustLocationAndSize(Screen screen)
        {
            if (screen != null)
            {
                DataGridViewElementStates states = DataGridViewElementStates.None;
                dgv.AutoResizeRows();
                int height = dgv.Rows.GetRowsHeight(states);
                int heightMax = screen.Bounds.Height -
                    new WindowsTaskbar().Size.Height -
                    labelTitle.Height;
                if (height > heightMax)
                {
                    height = heightMax;
                }
                dgv.Height = height;
                AdjustDataGridViewSize();
                int x = screen.Bounds.Right - Width;
                int y = heightMax - Height + labelTitle.Height;

                Location = new Point(x, y);
            }
        }

        internal void AdjustLocationAndSize(int heightMax, Menu menuPredecessor)
        {
            DataGridViewElementStates states = DataGridViewElementStates.None;
            if (!autoResizeRowsDone)
            {
                autoResizeRowsDone = true;
                this.dgv.AutoResizeRows();
            }
            int height = this.dgv.Rows.GetRowsHeight(states);
            if (height > heightMax)
            {
                height = heightMax;
            }
            this.dgv.Height = height;

            AdjustDataGridViewSize();
            int x = menuPredecessor.Location.X - Width +
                (int)Math.Round(Scaling.Factor, 0,
                MidpointRounding.AwayFromZero);

            RowData trigger = (RowData)Tag;
            DataGridView dgv = menuPredecessor.GetDataGridView();
            if (dgv.Rows.Count > trigger.RowIndex)
            {
                Rectangle cellRectangle = dgv.GetCellDisplayRectangle(
                    0, trigger.RowIndex, false);
                int y = menuPredecessor.Location.Y +
                    menuPredecessor.dgv.Location.Y +
                    cellRectangle.Top;
                if ((y + Height) > heightMax)
                {
                    y = heightMax - Height;
                }

                Location = new Point(x, y);
            }
        }

        private void AdjustDataGridViewSize()
        {
            //dgv.AutoResizeColumns(); //AutoResizeColumns slow ~45ms
            DataGridViewExtensions.FastAutoSizeColumns(dgv);

            bool scrollbarShown = false;
            foreach (VScrollBar scroll in dgv.Controls.OfType<VScrollBar>())
            {
                if (scroll.Visible)
                {
                    scroll.Width = 120;
                    scrollbarShown = true;
                }
            }
            int newWidth = dgv.Columns[0].Width + dgv.Columns[1].Width;
            if (scrollbarShown)
            {
                newWidth += SystemInformation.VerticalScrollBarWidth;
            }
            dgv.Width = newWidth;
        }

        private void dgv_MouseWheel(object sender, MouseEventArgs e)
        {
            ((HandledMouseEventArgs)e).Handled = true;
            int scrollspeed = MenuDefines.Scrollspeed;
            if (e.Delta < 0)
            {
                if (dgv.FirstDisplayedScrollingRowIndex < dgv.Rows.Count - scrollspeed)
                {
                    dgv.FirstDisplayedScrollingRowIndex += scrollspeed;
                }
                else
                {
                    dgv.FirstDisplayedScrollingRowIndex = dgv.Rows.Count - 1;
                }
            }
            else
            {
                if (dgv.FirstDisplayedScrollingRowIndex > 0 + scrollspeed)
                {
                    dgv.FirstDisplayedScrollingRowIndex -= scrollspeed;
                }
                else
                {
                    dgv.FirstDisplayedScrollingRowIndex = 0;
                }
            }

            MouseWheel.Invoke();
        }

        private void Menu_Deactivate(object sender, EventArgs e)
        {
            Deactivated?.Invoke();
        }

        internal void SetTitleColorDeactive()
        {
            labelTitle.ForeColor = Color.LightGray;
        }

        internal void SetTitleColorActive()
        {
            labelTitle.ForeColor = Color.Black;
        }

        private void LabelTitle_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                UserClickedOpenFolder?.Invoke();
            }
        }

        private void LabelTitle_MouseEnter(object sender, EventArgs e)
        {
            labelTitle.BackColor = MenuDefines.ColorTitleSelected;
        }

        private void LabelTitle_MouseLeave(object sender, EventArgs e)
        {
            labelTitle.BackColor = MenuDefines.ColorTitleBackground;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams Params = base.CreateParams;
                Params.ExStyle |= 0x80;
                return Params;
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keys)
        {
            switch (keys)
            {
                case Keys.Enter:
                case Keys.Up:
                case Keys.Down:
                case Keys.Left:
                case Keys.Right:
                case Keys.Escape:
                    CmdKeyProcessed.Invoke(keys);
                    return true;
                default:
                    break;
            }
            return base.ProcessCmdKey(ref msg, keys);
        }
    }
}