using System;
using System.Drawing;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using FreeBodySolver.Sections;
using FreeBodySolver.Models;
using FreeBodySolver.Core;

namespace FreeBodySolver.UI
{
    public class SectionViewerForm : Form
    {
        private PictureBox canvas;
        private SectionGenerator section;
        private DataGridView dgvProperties;
        private ListBox lstLibrary;

        // Üst panel elemanları
        private ComboBox cmbShapeType;
        private TextBox txtA, txtB, txtC, txtR, txtT, txtProfileName;
        private Button btnSaveProfile;
        private CheckBox chkDoubleProfile;
        private TextBox txtMirrorOffset;

        private float zoom = 1f;
        private PointF pan = new PointF(0, 0);
        private bool isUpdatingUI = false;
        private float displayedCx = 0f;
        private float displayedCy = 0f;
        private List<PointF> mirroredNodes = null;

        private float axesX = 0f;
        private float axesY = 0f;

        public SectionViewerForm()
        {
            this.Text = "Kesit Kütüphanesi ve Görünümü";
            this.Size = new Size(1100, 750);
            this.StartPosition = FormStartPosition.CenterParent;

            section = new SectionGenerator();

            // Önce Üst Paneli Ekliyoruz
            InitializeTopPanel();

            // Ana bölücü: Sol (Çizim) - Sağ (Kütüphane ve Özellikler)
            SplitContainer splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 700
            };

            canvas = new PictureBox { Dock = DockStyle.Fill, BackColor = Color.White };
            canvas.Paint += Canvas_Paint;
            canvas.Resize += (s, e) => { ZoomExtent(); canvas.Invalidate(); };

            canvas.MouseWheel += (s, e) =>
            {
                if (section.Nodes.Count == 0) return;
                var mousePt = e.Location;
                var worldBefore = new PointF((mousePt.X - pan.X) / zoom, (pan.Y - mousePt.Y) / zoom);
                float factor = (e.Delta > 0) ? 1.15f : (1f / 1.15f);
                zoom *= factor;
                pan = new PointF(mousePt.X - worldBefore.X * zoom, mousePt.Y + worldBefore.Y * zoom);
                canvas.Invalidate();
            };
            canvas.MouseEnter += (s, e) => canvas.Focus();

            // Sağ Taraf Bölücüsü: Üst (Kütüphane) - Alt (Hesaplanan Özellikler)
            SplitContainer splitRight = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 250
            };

            // Kütüphane Listesi
            lstLibrary = new ListBox { Dock = DockStyle.Fill, Font = new Font(this.Font.FontFamily, 10f) };
            lstLibrary.SelectedIndexChanged += LstLibrary_SelectedIndexChanged;

            // Silme işlemi için sağ tık menüsü
            ContextMenuStrip ctxLibrary = new ContextMenuStrip();
            var miDelete = new ToolStripMenuItem("Sil (Delete)");
            miDelete.Click += (s, e) =>
            {
                if (lstLibrary.SelectedItem != null)
                {
                    SectionDatabase.DeleteProfile(lstLibrary.SelectedItem.ToString());
                    RefreshLibraryList();
                }
            };
            ctxLibrary.Items.Add(miDelete);
            lstLibrary.ContextMenuStrip = ctxLibrary;

            GroupBox grpLibrary = new GroupBox { Text = "Kayıtlı Kesitler", Dock = DockStyle.Fill };
            grpLibrary.Controls.Add(lstLibrary);

            // Özellikler Tablosu
            dgvProperties = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.WhiteSmoke
            };
            dgvProperties.Columns.Add("Prop", "Özellik / Düğüm");
            dgvProperties.Columns.Add("Val", "Değer");

            GroupBox grpProps = new GroupBox { Text = "Kesit Özellikleri", Dock = DockStyle.Fill };
            grpProps.Controls.Add(dgvProperties);

            splitRight.Panel1.Controls.Add(grpLibrary);
            splitRight.Panel2.Controls.Add(grpProps);

            splitMain.Panel1.Controls.Add(canvas);
            splitMain.Panel2.Controls.Add(splitRight);

            this.Controls.Add(splitMain);

            // ÇÖZÜM 1: Üst menü ile gövdenin birbirini yutmasını/örtmesini engelliyoruz (Z-Order düzeltmesi)
            splitMain.BringToFront();

            RefreshLibraryList();
            GenerateSection();
        }

        // ÇÖZÜM 2: Form tamamen ekrana çizildikten sonra Zoom işlemi yapıp tam ortaya oturtuyoruz
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ZoomExtent();
            canvas.Invalidate();
        }

        private void InitializeTopPanel()
        {
            // Yüksekliği iki satır rahat sığacak şekilde 100'e çıkardık
            Panel pnlTop = new Panel { Dock = DockStyle.Top, Height = 100 };
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(5) };

            Label lblShape = new Label { Text = "Tip:", AutoSize = true, Margin = new Padding(3, 8, 3, 3) };
            cmbShapeType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
            cmbShapeType.Items.AddRange(new object[] { "Lipped C Shape", "U Shape" });
            cmbShapeType.SelectedIndex = 0;

            cmbShapeType.SelectedIndexChanged += (s, e) =>
            {
                txtC.Enabled = cmbShapeType.SelectedIndex == 0;
            };

            Label lblA = new Label { Text = "A:", AutoSize = true, Margin = new Padding(10, 8, 3, 3) };
            txtA = new TextBox { Width = 45, Text = "90" };

            Label lblB = new Label { Text = "B:", AutoSize = true, Margin = new Padding(10, 8, 3, 3) };
            txtB = new TextBox { Width = 45, Text = "50" };

            Label lblC = new Label { Text = "C:", AutoSize = true, Margin = new Padding(10, 8, 3, 3) };
            txtC = new TextBox { Width = 45, Text = "15" };

            Label lblR = new Label { Text = "R:", AutoSize = true, Margin = new Padding(10, 8, 3, 3) };
            txtR = new TextBox { Width = 45, Text = "2.5" };

            Label lblT = new Label { Text = "t:", AutoSize = true, Margin = new Padding(10, 8, 3, 3) };
            txtT = new TextBox { Width = 45, Text = "1.5" };

            // Double profile checkbox and offset (default gap = 5 mm)
            chkDoubleProfile = new CheckBox { Text = "Çift Profil (Mirror)", AutoSize = true, Margin = new Padding(15, 8, 3, 3) };
            Label lblOffset = new Label { Text = "Gap (mm):", AutoSize = true, Margin = new Padding(10, 8, 3, 3) };
            txtMirrorOffset = new TextBox { Width = 50, Text = "5" };

            // GenerateSection will be called automatically when inputs change; no explicit Draw button

            // Kaydetme Grubu
            Label lblName = new Label { Text = "Profil Adı:", AutoSize = true, Margin = new Padding(30, 8, 3, 3), Font = new Font(this.Font, FontStyle.Bold) };
            txtProfileName = new TextBox { Width = 140, Text = "C90x50x15x1.5" };
            btnSaveProfile = new Button { Text = "Kaydet", Width = 80, BackColor = Color.LightSkyBlue, Margin = new Padding(3, 3, 3, 3) };
            btnSaveProfile.Click += BtnSaveProfile_Click;

            flow.Controls.AddRange(new Control[] {
                lblShape, cmbShapeType,
                lblA, txtA, lblB, txtB, lblC, txtC, lblR, txtR, lblT, txtT,
                chkDoubleProfile, lblOffset, txtMirrorOffset,
                lblName, txtProfileName, btnSaveProfile
            });

            // Auto-update when inputs change
            cmbShapeType.SelectedIndexChanged += (s, e) => { txtC.Enabled = cmbShapeType.SelectedIndex == 0; GenerateSection(); };
            txtA.TextChanged += (s, e) => GenerateSection();
            txtB.TextChanged += (s, e) => GenerateSection();
            txtC.TextChanged += (s, e) => GenerateSection();
            txtR.TextChanged += (s, e) => GenerateSection();
            txtT.TextChanged += (s, e) => GenerateSection();
            chkDoubleProfile.CheckedChanged += (s, e) => GenerateSection();
            txtMirrorOffset.TextChanged += (s, e) => GenerateSection();

            pnlTop.Controls.Add(flow);
            this.Controls.Add(pnlTop);
        }

        private void RefreshLibraryList()
        {
            isUpdatingUI = true;
            lstLibrary.Items.Clear();
            var profiles = SectionDatabase.LoadProfiles();
            foreach (var p in profiles)
            {
                lstLibrary.Items.Add(p.Name);
            }
            isUpdatingUI = false;
        }

        private void LstLibrary_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (isUpdatingUI || lstLibrary.SelectedItem == null) return;

            string selectedName = lstLibrary.SelectedItem.ToString();
            var profile = SectionDatabase.LoadProfiles().FirstOrDefault(x => x.Name == selectedName);

            if (profile != null)
            {
                cmbShapeType.SelectedItem = profile.ShapeType;
                txtA.Text = profile.A.ToString(CultureInfo.InvariantCulture);
                txtB.Text = profile.B.ToString(CultureInfo.InvariantCulture);
                txtC.Text = profile.C.ToString(CultureInfo.InvariantCulture);
                txtR.Text = profile.R.ToString(CultureInfo.InvariantCulture);
                txtT.Text = profile.t.ToString(CultureInfo.InvariantCulture);
                txtProfileName.Text = profile.Name;

                GenerateSection();
            }
        }

        private void BtnSaveProfile_Click(object sender, EventArgs e)
        {
            string pName = txtProfileName.Text.Trim();
            if (string.IsNullOrEmpty(pName))
            {
                MessageBox.Show("Lütfen bir profil adı girin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // --- YENİ EKLENEN KONTROL (Aynı İsimde Kayıt Var mı?) ---
            var existingProfiles = SectionDatabase.LoadProfiles();
            // Büyük/küçük harf duyarsız (case-insensitive) olarak isimleri karşılaştırıyoruz
            if (existingProfiles.Any(x => x.Name.Equals(pName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"'{pName}' isminde bir kesit kütüphanede zaten mevcut!\nLütfen üzerine yazmamak için farklı bir isim belirleyin.", "İsim Çakışması", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return; // Kaydetme işlemini burada iptal et
            }
            // ---------------------------------------------------------

            var profile = new SectionProfile
            {
                Name = pName,
                ShapeType = cmbShapeType.SelectedItem.ToString(),
                A = ParseVal(txtA, 90f),
                B = ParseVal(txtB, 50f),
                C = ParseVal(txtC, 15f),
                R = ParseVal(txtR, 2.5f),
                t = ParseVal(txtT, 1.5f)
            };

            SectionDatabase.SaveProfile(profile);
            RefreshLibraryList();

            MessageBox.Show($"'{pName}' kütüphaneye başarıyla kaydedildi.", "Kayıt Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private float ParseVal(TextBox txt, float defaultVal)
        {
            if (float.TryParse(txt.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out float result))
                return result;
            return defaultVal;
        }

        private void GenerateSection()
        {
            float A = ParseVal(txtA, 90f);
            float B = ParseVal(txtB, 50f);
            float C = ParseVal(txtC, 15f);
            float R = ParseVal(txtR, 2.5f);
            float t = ParseVal(txtT, 1.5f);

            section = new SectionGenerator();

            if (cmbShapeType.SelectedIndex == 0) // Lipped C
                section.GenerateLippedC(A, B, C, t, R, 2, 4, 4, 3);
            else // U Shape
                section.GenerateUShape(A, B, t, R, 4, 4, 3);

            // Tablo değerleri için orijinal veriyi saklıyoruz
            displayedCx = section.OriginalCx;
            displayedCy = section.OriginalCy;
            mirroredNodes = null;

            // Tekli profilin yerel ağırlık merkezi orijindedir
            axesX = 0f;
            axesY = 0f;

            // --- ÇİFT PROFİL (MIRROR) HESAPLAMASI ---
            if (chkDoubleProfile != null && chkDoubleProfile.Checked && (cmbShapeType.SelectedIndex == 0 || cmbShapeType.SelectedIndex == 1))
            {
                float offsetMm = ParseVal(txtMirrorOffset, 0f);
                float bestLen = 0f; float webX = float.NaN;
                for (int i = 0; i < section.Nodes.Count - 1; i++)
                {
                    var p1 = section.Nodes[i]; var p2 = section.Nodes[i + 1];
                    float dx = p2.X - p1.X; float dy = p2.Y - p1.Y;
                    float len = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (len <= 1e-6f) continue;
                    if (Math.Abs(dx) < Math.Abs(dy) * 0.2f)
                    {
                        if (len > bestLen) { bestLen = len; webX = (p1.X + p2.X) / 2f; }
                    }
                }

                if (!float.IsNaN(webX))
                {
                    float mirroredWebCenterX = webX - (t + offsetMm);
                    float planeX = (webX + mirroredWebCenterX) / 2f;

                    // Aynalanmış noktaları oluşturuyoruz
                    mirroredNodes = section.Nodes.Select(n => new PointF(2f * planeX - n.X, n.Y)).ToList();

                    try
                    {
                        Func<List<PointF>, PointF> polyCentroid = (poly) =>
                        {
                            double Area = 0; double Cx = 0; double Cy = 0;
                            for (int i = 0; i < poly.Count; i++)
                            {
                                var p1 = poly[i]; var p2 = poly[(i + 1) % poly.Count];
                                double cross = p1.X * p2.Y - p2.X * p1.Y;
                                Area += cross; Cx += (p1.X + p2.X) * cross; Cy += (p1.Y + p2.Y) * cross;
                            }
                            Area *= 0.5; if (Math.Abs(Area) < 1e-6) return new PointF(0, 0);
                            Cx /= (6 * Area); Cy /= (6 * Area);
                            return new PointF((float)Cx, (float)Cy);
                        };
                        var c1 = polyCentroid(section.Nodes);
                        var c2 = polyCentroid(mirroredNodes);

                        Func<List<PointF>, double> polyArea = (poly) =>
                        {
                            double Area = 0;
                            for (int i = 0; i < poly.Count; i++)
                            {
                                var p1 = poly[i]; var p2 = poly[(i + 1) % poly.Count];
                                Area += p1.X * p2.Y - p2.X * p1.Y;
                            }
                            return Math.Abs(Area) * 0.5;
                        };
                        double A1 = polyArea(section.Nodes); double A2 = polyArea(mirroredNodes);

                        // Çift profilin yeni birleşik ağırlık merkezini atıyoruz
                        axesX = (float)((A1 * c1.X + A2 * c2.X) / (A1 + A2));
                        axesY = (float)((A1 * c1.Y + A2 * c2.Y) / (A1 + A2));
                    }
                    catch { }
                }
            }

            LoadPropertiesToGrid();
            ZoomExtent();
            canvas.Invalidate();
        }

        private void LoadPropertiesToGrid()
        {
            dgvProperties.Rows.Clear();
            dgvProperties.Rows.Add("Alan (A)", $"{section.Area:0.00} mm²");
            dgvProperties.Rows.Add("Ağırlık Mer. X (Cx)", $"{displayedCx:0.00} mm");
            dgvProperties.Rows.Add("Ağırlık Mer. Y (Cy)", $"{displayedCy:0.00} mm");
            dgvProperties.Rows.Add("Atalet (Ixx)", $"{section.Ixx:0.00} mm⁴");
            dgvProperties.Rows.Add("Atalet (Iyy)", $"{section.Iyy:0.00} mm⁴");
            dgvProperties.Rows.Add("Toplam Node Sayısı", $"{section.Nodes.Count}");
            dgvProperties.Rows.Add("---", "---");

            for (int i = 0; i < section.Nodes.Count; i++)
            {
                dgvProperties.Rows.Add($"Node {i + 1}", $"X: {section.Nodes[i].X:0.00} | Y: {section.Nodes[i].Y:0.00}");
            }
        }

        private void ZoomExtent()
        {
            if (section == null || section.Nodes.Count == 0) return;

            float minX = section.Nodes.Min(n => n.X);
            float maxX = section.Nodes.Max(n => n.X);
            float minY = section.Nodes.Min(n => n.Y);
            float maxY = section.Nodes.Max(n => n.Y);

            // Çift profil varsa ekranı ona göre genişlet
            if (mirroredNodes != null && mirroredNodes.Count > 0)
            {
                minX = Math.Min(minX, mirroredNodes.Min(n => n.X));
                maxX = Math.Max(maxX, mirroredNodes.Max(n => n.X));
                minY = Math.Min(minY, mirroredNodes.Min(n => n.Y));
                maxY = Math.Max(maxY, mirroredNodes.Max(n => n.Y));
            }

            if (Math.Abs(maxX - minX) < 1e-3f) { minX -= 10; maxX += 10; }
            if (Math.Abs(maxY - minY) < 1e-3f) { minY -= 10; maxY += 10; }

            int w = canvas.ClientSize.Width;
            int h = canvas.ClientSize.Height;
            if (w <= 0) w = 600;
            if (h <= 0) h = 400;

            float margin = 60f;
            float drawW = Math.Max(1f, w - 2 * margin);
            float drawH = Math.Max(1f, h - 2 * margin);

            float sx = drawW / (maxX - minX);
            float sy = drawH / (maxY - minY);

            zoom = Math.Min(sx, sy);
            if (zoom <= 0.001f) zoom = 0.001f;

            float cx = (minX + maxX) / 2f;
            float cy = (minY + maxY) / 2f;

            pan = new PointF(w / 2f - cx * zoom, h / 2f + cy * zoom);
        }

        private void Canvas_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (section == null || section.Nodes.Count < 2) return;

            Func<PointF, PointF> toPix = (world) => new PointF(pan.X + world.X * zoom, pan.Y - world.Y * zoom);

            // 1. ÇİFT PROFİL ÇİZİMİ (VARSA)
            if (mirroredNodes != null)
            {
                using (var penM = new Pen(Color.OrangeRed, 2f))
                {
                    for (int i = 0; i < mirroredNodes.Count - 1; i++)
                    {
                        g.DrawLine(penM, toPix(mirroredNodes[i]), toPix(mirroredNodes[i + 1]));
                    }
                }
                foreach (var mn in mirroredNodes)
                {
                    PointF p = toPix(mn);
                    g.FillEllipse(Brushes.OrangeRed, p.X - 2, p.Y - 2, 4, 4);
                }
            }

            // 2. ORİJİNAL PROFİL ÇİZİMİ (HER ZAMAN ÇİZİLİR)
            using (var pen = new Pen(Color.DarkBlue, 2f))
            {
                for (int i = 0; i < section.Nodes.Count - 1; i++)
                {
                    g.DrawLine(pen, toPix(section.Nodes[i]), toPix(section.Nodes[i + 1]));
                }
            }
            foreach (var node in section.Nodes)
            {
                PointF p = toPix(node);
                g.FillEllipse(Brushes.Red, p.X - 3, p.Y - 3, 6, 6);
            }

            // 3. EKSEN TAKIMININ ÇİZİLMESİ (TEK VEYA ÇİFT PROFİLİN ORTAK CG NOKTASINDA)
            try
            {
                float axisLen = 60f;
                PointF cgPix = toPix(new PointF(axesX, axesY));

                using (var penY = new Pen(Color.Green, 3f))
                {
                    g.DrawLine(penY, cgPix.X, cgPix.Y, cgPix.X, cgPix.Y - axisLen);
                    g.FillPolygon(Brushes.Green, new PointF[] { new PointF(cgPix.X, cgPix.Y - axisLen - 8), new PointF(cgPix.X - 6, cgPix.Y - axisLen + 6), new PointF(cgPix.X + 6, cgPix.Y - axisLen + 6) });
                }
                g.DrawString("Y (up, 0°)", this.Font, Brushes.Green, cgPix.X + 6, cgPix.Y - axisLen - 18);

                using (var penZ = new Pen(Color.Blue, 3f))
                {
                    g.DrawLine(penZ, cgPix.X, cgPix.Y, cgPix.X + axisLen, cgPix.Y);
                    g.FillPolygon(Brushes.Blue, new PointF[] { new PointF(cgPix.X + axisLen + 8, cgPix.Y), new PointF(cgPix.X + axisLen - 6, cgPix.Y - 6), new PointF(cgPix.X + axisLen - 6, cgPix.Y + 6) });
                }
                g.DrawString("Z (right)", this.Font, Brushes.Blue, cgPix.X + axisLen + 10, cgPix.Y + 4);
            }
            catch { }
        }
    }
}