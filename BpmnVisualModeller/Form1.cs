using BpmnVisualModeller.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
namespace BpmnVisualModeller
{
    public partial class Form1 : Form
    {
        private const int GATEWAY_SIZE = 40;

        private MenuStrip menuStrip;
        private ToolStrip toolStrip;
        private StatusStrip statusStrip;
        private SplitContainer mainSplitContainer;
        private Panel canvasPanel;
        private PropertyGrid propertyGrid;
        private TabControl rightTabControl;
        private ListBox logListBox;
        private DataGridView variablesGridView;

        private ToolStripButton btnLoadXml;
        private ToolStripButton btnStartSimulation;
        private ToolStripButton btnStepSimulation;
        private ToolStripButton btnResetSimulation;
        private ToolStripButton btnPauseSimulation;
        private ToolStripButton btnZoomIn;
        private ToolStripButton btnZoomOut;
        private ToolStripButton btnFitToScreen;
        private ToolStripComboBox cmbSpeed;
        private ToolStripButton btnAutoStep;
        private ToolStripButton btnStopAll;

        private Timer simulationTimer;

        private BpmnProcess _currentProcess;
        private BpmnSimulator _simulator;
        private List<Token> _activeTokens = new List<Token>();

        private List<BpmnPool> _pools = new List<BpmnPool>();
        private List<BpmnLane> _lanes = new List<BpmnLane>();


        private Dictionary<string, Point> nodePositions;
        private Dictionary<string, Rectangle> nodeRectangles;
        private Dictionary<string, List<Point>> flowPaths;
        private float _zoom = 1.0f;
        private Point _dragStartPoint;
        private bool _isDragging;
        private int _simulationStep;
        private bool _isSimulating;

        private int _diagramWidth = 1200;
        private int _diagramHeight = 800;
        private bool _useBpmnDiLayout;

        private Dictionary<string, string> _gatewaySelectedFlows;
        private Dictionary<string, HashSet<string>> _gatewayInclusiveSelectedFlows;
        private readonly Dictionary<string, List<GatewayFlowButton>> _gatewayFlowButtons =
            new Dictionary<string, List<GatewayFlowButton>>();

        private Timer _animationTimer;
        private Dictionary<string, float> _pulseValues = new Dictionary<string, float>();

        public Form1()
        {
            this.Text = "BPMN Visual Modeller - Симулятор бизнес-процессов";
            this.Size = new Size(1400, 900);
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = Color.White;

            InitializeCustomControls();
            InitializeDataStructures();
            InitializeAnimations();
            this.Resize += Form1_Resize;
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (canvasPanel != null)
            {
                UpdateCanvasSize();
            }
        }

        private void UpdateCanvasSize()
        {
            int width = Math.Max(_diagramWidth + 200, mainSplitContainer.Panel1.Width - 20);
            int height = Math.Max(_diagramHeight, mainSplitContainer.Panel1.Height - 20);
            canvasPanel.Size = new Size(width, height);
            canvasPanel.Invalidate();
        }

        private void InitializeDataStructures()
        {
            nodePositions = new Dictionary<string, Point>();
            nodeRectangles = new Dictionary<string, Rectangle>();
            flowPaths = new Dictionary<string, List<Point>>();
            _gatewaySelectedFlows = new Dictionary<string, string>();
            _gatewayInclusiveSelectedFlows = new Dictionary<string, HashSet<string>>();
        }

        private void InitializeCustomControls()
        {
            // MenuStrip
            menuStrip = new MenuStrip();
            var fileMenu = new ToolStripMenuItem("Файл");
            var loadItem = new ToolStripMenuItem("Загрузить BPMN...", null, LoadXml_Click);
            var exitItem = new ToolStripMenuItem("Выход", null, (s, e) => Application.Exit());
            fileMenu.DropDownItems.Add(loadItem);
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(exitItem);

            var simulationMenu = new ToolStripMenuItem("Симуляция");
            var startItem = new ToolStripMenuItem("Запустить", null, StartSimulation_Click);
            var stepItem = new ToolStripMenuItem("Шаг", null, StepSimulation_Click);
            var resetItem = new ToolStripMenuItem("Сброс", null, ResetSimulation_Click);
            simulationMenu.DropDownItems.Add(startItem);
            simulationMenu.DropDownItems.Add(stepItem);
            simulationMenu.DropDownItems.Add(resetItem);

            var viewMenu = new ToolStripMenuItem("Вид");
            var zoomInItem = new ToolStripMenuItem("Увеличить", null, (s, e) => ZoomIn());
            var zoomOutItem = new ToolStripMenuItem("Уменьшить", null, (s, e) => ZoomOut());
            var fitToScreenItem = new ToolStripMenuItem("По размеру окна", null, (s, e) => FitToScreen());
            var resetZoomItem = new ToolStripMenuItem("Сбросить масштаб (100%)", null, (s, e) => ResetZoom());
            viewMenu.DropDownItems.Add(zoomInItem);
            viewMenu.DropDownItems.Add(zoomOutItem);
            viewMenu.DropDownItems.Add(fitToScreenItem);
            viewMenu.DropDownItems.Add(resetZoomItem);

            menuStrip.Items.Add(fileMenu);
            menuStrip.Items.Add(simulationMenu);
            menuStrip.Items.Add(viewMenu);

            // ToolStrip
            toolStrip = new ToolStrip();
            btnLoadXml = new ToolStripButton("📁 Загрузить BPMN", null, LoadXml_Click);
            btnStartSimulation = new ToolStripButton("▶ Новый экземпляр", null, (s, e) => StartNewProcessInstance());
            btnStepSimulation = new ToolStripButton("⏯ Шаг", null, StepSimulation_Click);
            btnPauseSimulation = new ToolStripButton("⏸ Пауза", null, (s, e) => PauseSimulation());
            btnResetSimulation = new ToolStripButton("🔄 Сброс", null, ResetSimulation_Click);
            btnZoomIn = new ToolStripButton("🔍+", null, (s, e) => ZoomIn());
            btnZoomOut = new ToolStripButton("🔍-", null, (s, e) => ZoomOut());
            btnFitToScreen = new ToolStripButton("📐 По размеру", null, (s, e) => FitToScreen());

            toolStrip.Items.Add(btnLoadXml);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(btnStartSimulation);
            toolStrip.Items.Add(btnStepSimulation);
            toolStrip.Items.Add(btnPauseSimulation);
            toolStrip.Items.Add(btnResetSimulation);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(btnZoomIn);
            toolStrip.Items.Add(btnZoomOut);
            toolStrip.Items.Add(btnFitToScreen);
            toolStrip.Items.Add(new ToolStripSeparator());
            toolStrip.Items.Add(new ToolStripLabel("Скорость (мс):"));

            cmbSpeed = new ToolStripComboBox();
            cmbSpeed.Items.AddRange(new object[] { "100", "300", "500", "800", "1000", "2000" });
            cmbSpeed.SelectedItem = "500";
            cmbSpeed.Width = 60;
            toolStrip.Items.Add(cmbSpeed);

            btnAutoStep = new ToolStripButton("🤖 Автошаг");
            btnAutoStep.CheckOnClick = true;
            btnAutoStep.CheckedChanged += BtnAutoStep_CheckedChanged;
            toolStrip.Items.Add(btnAutoStep);

            btnStopAll = new ToolStripButton("⏹ Стоп все", null, (s, e) => StopAllProcesses());
            toolStrip.Items.Add(btnStopAll);

            // StatusStrip
            statusStrip = new StatusStrip();
            statusStrip.Items.Add(new ToolStripStatusLabel("Готов"));
            statusStrip.Items.Add(new ToolStripStatusLabel("|"));
            statusStrip.Items.Add(new ToolStripStatusLabel("Загрузите BPMN файл для начала"));
            statusStrip.Items.Add(new ToolStripStatusLabel("|"));
            statusStrip.Items.Add(new ToolStripStatusLabel($"Масштаб: {_zoom * 100:F0}%"));

            // Main SplitContainer
            mainSplitContainer = new SplitContainer();
            mainSplitContainer.Dock = DockStyle.Fill;
            mainSplitContainer.Orientation = Orientation.Vertical;
            mainSplitContainer.SplitterDistance = 900;
            mainSplitContainer.SplitterWidth = 5;

            // Canvas Panel
            canvasPanel = new Panel();
            canvasPanel.BackColor = Color.FromArgb(240, 240, 240);
            canvasPanel.AutoScroll = true;
            canvasPanel.AutoScrollMinSize = new Size(1200, 800);
            canvasPanel.Paint += CanvasPanel_Paint;
            canvasPanel.MouseDown += CanvasPanel_MouseDown;
            canvasPanel.MouseMove += CanvasPanel_MouseMove;
            canvasPanel.MouseUp += CanvasPanel_MouseUp;
            canvasPanel.MouseClick += CanvasPanel_MouseClick;
            canvasPanel.GetType().GetProperty("DoubleBuffered",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(canvasPanel, true, null);

            // Right Panel
            rightTabControl = new TabControl();
            rightTabControl.Dock = DockStyle.Fill;

            // PropertyGrid tab
            propertyGrid = new PropertyGrid();
            propertyGrid.PropertySort = PropertySort.Alphabetical;
            var propertiesTab = new TabPage("Свойства");
            propertiesTab.Controls.Add(propertyGrid);

            // Log tab
            logListBox = new ListBox();
            logListBox.Font = new Font("Consolas", 9);
            logListBox.Dock = DockStyle.Fill;
            logListBox.BackColor = Color.Black;
            logListBox.ForeColor = Color.LightGreen;
            var logTab = new TabPage("Лог выполнения");
            logTab.Controls.Add(logListBox);

            // Variables tab
            variablesGridView = new DataGridView();
            variablesGridView.Dock = DockStyle.Fill;
            variablesGridView.AllowUserToAddRows = true;
            variablesGridView.AllowUserToDeleteRows = true;
            variablesGridView.ColumnCount = 2;
            variablesGridView.Columns[0].Name = "Переменная";
            variablesGridView.Columns[1].Name = "Значение";
            variablesGridView.Columns[0].Width = 150;
            variablesGridView.Columns[1].Width = 150;
            var variablesTab = new TabPage("Переменные");
            variablesTab.Controls.Add(variablesGridView);

            variablesGridView.Rows.Add("score", "750");
            variablesGridView.Rows.Add("amount", "10000");
            variablesGridView.Rows.Add("approved", "true");

            rightTabControl.TabPages.Add(propertiesTab);
            rightTabControl.TabPages.Add(variablesTab);
            rightTabControl.TabPages.Add(logTab);

            mainSplitContainer.Panel1.Controls.Add(canvasPanel);
            mainSplitContainer.Panel2.Controls.Add(rightTabControl);

            this.Controls.Add(mainSplitContainer);
            this.Controls.Add(toolStrip);
            this.Controls.Add(menuStrip);
            this.Controls.Add(statusStrip);

            toolStrip.Dock = DockStyle.Top;
            menuStrip.Dock = DockStyle.Top;
            statusStrip.Dock = DockStyle.Bottom;

            simulationTimer = new Timer();
            simulationTimer.Tick += SimulationTimer_Tick;

            SetButtonsEnabled(false);
        }

        private void SetButtonsEnabled(bool hasProcessLoaded)
        {
            btnLoadXml.Enabled = true;
            btnStartSimulation.Enabled = hasProcessLoaded;
            btnStepSimulation.Enabled = hasProcessLoaded && _activeTokens != null && _activeTokens.Any(t => !t.IsCompleted);
            btnResetSimulation.Enabled = hasProcessLoaded && _activeTokens != null && _activeTokens.Count > 0;
            btnPauseSimulation.Enabled = hasProcessLoaded && _activeTokens != null && _activeTokens.Any(t => !t.IsCompleted);
            btnAutoStep.Enabled = hasProcessLoaded && _activeTokens != null && _activeTokens.Any(t => !t.IsCompleted);
        }

        private void BtnAutoStep_CheckedChanged(object sender, EventArgs e)
        {
            if (btnAutoStep.Checked)
            {
                if (_simulator != null && _activeTokens != null && _activeTokens.Any(t => !t.IsCompleted))
                {
                    int speed = GetSelectedSpeed();
                    if (speed > 0)
                    {
                        simulationTimer.Interval = speed;
                        simulationTimer.Start();
                        AddLogMessage($"🤖 Автошаг ВКЛЮЧЕН (интервал: {speed} мс)");
                        UpdateStatus("Автошаг активен");
                    }
                    else
                    {
                        AddLogMessage("⚠️ Некорректное значение скорости, автошаг не запущен");
                        btnAutoStep.Checked = false;
                    }
                }
                else
                {
                    AddLogMessage("⚠️ Нет активных процессов для автошага");
                    btnAutoStep.Checked = false;
                }
            }
            else
            {
                if (simulationTimer.Enabled)
                {
                    simulationTimer.Stop();
                    AddLogMessage("⏸ Автошаг ВЫКЛЮЧЕН");
                    UpdateStatus("Автошаг остановлен");
                }
            }
        }

        private int GetSelectedSpeed()
        {
            if (cmbSpeed.SelectedItem == null)
                return 500;

            string speedText = cmbSpeed.SelectedItem.ToString();

            if (int.TryParse(speedText, out int speed))
                return speed;

            return 500;
        }

        private void LoadXml_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "BPMN files (*.bpmn;*.xml)|*.bpmn;*.xml|All files (*.*)|*.*";
                openFileDialog.Title = "Загрузить BPMN диаграмму";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        Cursor = Cursors.WaitCursor;
                        LoadBpmnFile(openFileDialog.FileName);
                        Cursor = Cursors.Default;
                        SetButtonsEnabled(true);
                        UpdateStatus($"Загружен процесс: {_currentProcess?.Name ?? "Без имени"}");
                    }
                    catch (Exception ex)
                    {
                        Cursor = Cursors.Default;
                        MessageBox.Show($"Ошибка загрузки: {ex.Message}\n{ex.StackTrace}", "Ошибка",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        UpdateStatus($"Ошибка: {ex.Message}");
                    }
                }
            }
        }

        private void LoadBpmnFile(string filePath)
        {
            var parser = new BpmnParser();
            var result = parser.ParseWithPools(filePath);
            _currentProcess = result.process;
            _pools = result.pools ?? new List<BpmnPool>();
            _lanes = result.lanes ?? new List<BpmnLane>();

            if (_currentProcess != null)
                BpmnPoolLayout.Normalize(_pools, _lanes, _currentProcess);

            _simulator = new BpmnSimulator(_currentProcess);
            SyncSimulatorGatewaySelections();

            _simulator.OnTokenMoved += Simulator_OnTokenMoved;
            _simulator.OnDecision += Simulator_OnDecision;
            _simulator.OnParallelSplit += Simulator_OnParallelSplit;
            _simulator.OnError += Simulator_OnError;
            _simulator.OnTokensUpdated += Simulator_OnTokensUpdated;

            _gatewaySelectedFlows.Clear();
            _gatewayInclusiveSelectedFlows.Clear();

            _useBpmnDiLayout = parser.HasDiagramLayout(_currentProcess);

            if (_useBpmnDiLayout)
            {
                ApplyDiagramInterchange(parser.ShapeBounds, parser.EdgeWaypoints);
            }
            else if (_pools != null && _pools.Any())
            {
                CalculateNodePositionsUsingPools();
                CalculateFlowPaths();
            }
            else
            {
                CalculateNodePositionsImproved();
                CalculateFlowPaths();
            }

            UpdateCanvasSize();
            canvasPanel.Invalidate();

            AddLogMessage($"✅ Процесс загружен: {_currentProcess.Name ?? _currentProcess.Id}");
            AddLogMessage($"📊 Найдено узлов: {_currentProcess.Nodes.Count}");
            AddLogMessage($"🔗 Найдено связей: {_currentProcess.OutgoingFlows.Values.Sum(f => f.Count)}");
            AddLogMessage($"📦 Найдено пулов: {_pools.Count}");
            AddLogMessage($"🛤️ Найдено дорожек: {_lanes.Count}");

            FitToScreen();
        }

        private void CanvasPanel_Paint(object sender, PaintEventArgs e)
        {
            if (_currentProcess == null || nodeRectangles.Count == 0)
            {
                DrawEmptyStateMessage(e.Graphics);
                return;
            }

            try
            {
                Graphics graphics = e.Graphics;
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TranslateTransform(canvasPanel.AutoScrollPosition.X, canvasPanel.AutoScrollPosition.Y);
                graphics.ScaleTransform(_zoom, _zoom);

                _gatewayFlowButtons.Clear();

                DrawGrid(graphics);

                DrawPoolsAndLanes(graphics);

                foreach (var flow in flowPaths)
                {
                    if (flow.Value != null && flow.Value.Count >= 2)
                        DrawFlow(graphics, flow.Value, flow.Key);
                }

                foreach (var node in _currentProcess.Nodes.Values)
                {
                    if (nodeRectangles.TryGetValue(node.Id, out Rectangle rect))
                        DrawNodeBpmnStandard(graphics, node, rect);
                }

                if (_activeTokens != null && _activeTokens.Count > 0)
                {
                    DrawAllTokens(graphics);
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"❌ Ошибка отрисовки: {ex.Message}");
            }
        }

        private void ApplyDiagramInterchange(
            Dictionary<string, Rectangle> shapeBounds,
            Dictionary<string, List<Point>> edgeWaypoints)
        {
            nodePositions.Clear();
            nodeRectangles.Clear();
            flowPaths.Clear();

            foreach (var node in _currentProcess.Nodes.Values)
            {
                if (!shapeBounds.TryGetValue(node.Id, out var rect) || rect.Width <= 0)
                    continue;

                nodeRectangles[node.Id] = rect;
                nodePositions[node.Id] = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            }

            foreach (var kvp in edgeWaypoints)
            {
                if (kvp.Value != null && kvp.Value.Count >= 2)
                    flowPaths[kvp.Key] = new List<Point>(kvp.Value);
            }

            UpdateDiagramSizeFromBounds(shapeBounds);
        }

        private void UpdateDiagramSizeFromBounds(Dictionary<string, Rectangle> shapeBounds)
        {
            int maxRight = 120;
            int maxBottom = 120;

            foreach (var rect in shapeBounds.Values)
            {
                maxRight = Math.Max(maxRight, rect.Right);
                maxBottom = Math.Max(maxBottom, rect.Bottom);
            }

            if (_pools != null)
            {
                foreach (var pool in _pools)
                {
                    maxRight = Math.Max(maxRight, pool.Bounds.Right);
                    maxBottom = Math.Max(maxBottom, pool.Bounds.Bottom);
                }
            }

            _diagramWidth = maxRight + 80;
            _diagramHeight = maxBottom + 80;
        }

        private void DrawPoolsAndLanes(Graphics g)
        {
            if (_pools == null || !_pools.Any())
                return;

            if (_useBpmnDiLayout)
            {
                DrawPoolsAndLanesBpmnStandard(g);
                return;
            }

            var state = g.Save();

            foreach (var pool in _pools)
            {
                using (var path = GetRoundedRectanglePath(pool.Bounds, 8))
                using (Brush poolBrush = new SolidBrush(Color.FromArgb(245, 250, 255)))
                using (Pen poolPen = new Pen(Color.SteelBlue, 2))
                {
                    g.FillPath(poolBrush, path);
                    g.DrawPath(poolPen, path);
                }

                using (Font headerFont = new Font("Segoe UI", 12, FontStyle.Bold))
                using (Brush textBrush = new SolidBrush(Color.DarkBlue))
                {
                    string poolName = string.IsNullOrEmpty(pool.Name) ? "Пул" : pool.Name;
                    PointF headerPosition = new PointF(pool.Bounds.X + 15, pool.Bounds.Y + 8);
                    g.DrawString(poolName, headerFont, textBrush, headerPosition);
                }

                using (Pen linePen = new Pen(Color.SteelBlue, 1))
                {
                    g.DrawLine(linePen,
                        pool.Bounds.X + 10,
                        pool.Bounds.Y + 35,
                        pool.Bounds.Right - 10,
                        pool.Bounds.Y + 35);
                }

                foreach (var lane in pool.Lanes.OrderBy(l => l.Order))
                {
                    Color laneColor = GetLaneColor(lane);
                    using (Brush laneBrush = new SolidBrush(laneColor))
                    using (Pen lanePen = new Pen(Color.LightSteelBlue, 1))
                    {
                        g.FillRectangle(laneBrush, lane.Bounds);
                        g.DrawRectangle(lanePen, lane.Bounds);
                    }

                    using (Pen separatorPen = new Pen(Color.SteelBlue, 2))
                    {
                        g.DrawLine(separatorPen,
                            lane.Bounds.X + 90,
                            lane.Bounds.Y,
                            lane.Bounds.X + 90,
                            lane.Bounds.Bottom);
                    }

                    using (Font laneFont = new Font("Segoe UI", 10, FontStyle.Bold))
                    using (Brush textBrush = new SolidBrush(Color.DarkSlateGray))
                    {
                        string laneName = string.IsNullOrEmpty(lane.Name) ? "Дорожка" : lane.Name;
                        RectangleF nameRect = new RectangleF(lane.Bounds.X + 10, lane.Bounds.Y + 10, 70, lane.Bounds.Height - 20);

                        g.TranslateTransform(nameRect.X + nameRect.Width / 2, nameRect.Y + nameRect.Height / 2);
                        g.RotateTransform(-90);

                        if (laneName.Length > 15)
                            laneName = laneName.Substring(0, 12) + "...";

                        using (var sf = new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center
                        })
                        {
                            g.DrawString(laneName, laneFont, textBrush, 0, 0, sf);
                        }

                        g.ResetTransform();
                    }
                }
            }

            for (int i = 0; i < _pools.Count - 1; i++)
            {
                var currentPool = _pools[i];
                var nextPool = _pools[i + 1];

                using (Pen separatorPen = new Pen(Color.SteelBlue, 3) { DashStyle = DashStyle.Dash })
                {
                    int separatorY = (currentPool.Bounds.Bottom + nextPool.Bounds.Top) / 2;
                    g.DrawLine(separatorPen,
                        Math.Min(currentPool.Bounds.X, nextPool.Bounds.X),
                        separatorY,
                        Math.Max(currentPool.Bounds.Right, nextPool.Bounds.Right),
                        separatorY);
                }
            }

            g.Restore(state);
        }

        private void DrawPoolsAndLanesBpmnStandard(Graphics g)
        {
            using (var borderPen = new Pen(Color.Black, 1))
            using (var bg = new SolidBrush(Color.White))
            {
                foreach (var pool in _pools)
                {
                    Rectangle poolRect = pool.Bounds;
                    g.FillRectangle(bg, poolRect);
                    g.DrawRectangle(borderPen, poolRect);

                    int labelWidth = GetPoolLabelStripWidth(pool);
                    if (labelWidth > 0)
                    {
                        int lineX = poolRect.X + labelWidth;
                        g.DrawLine(borderPen, lineX, poolRect.Y, lineX, poolRect.Bottom);

                        if (!string.IsNullOrWhiteSpace(pool.Name))
                            DrawVerticalPoolLabel(g, pool.Name, poolRect, labelWidth);
                    }

                    foreach (var lane in pool.Lanes.OrderBy(l => l.Bounds.Y))
                    {
                        if (lane.Bounds.Width <= 0 || lane.Bounds.Height <= 0)
                            continue;

                        g.FillRectangle(bg, lane.Bounds);
                        g.DrawRectangle(borderPen, lane.Bounds);

                        if (!string.IsNullOrWhiteSpace(lane.Name))
                            DrawHorizontalLaneLabel(g, lane, labelWidth);
                    }
                }
            }
        }

        private static int GetPoolLabelStripWidth(BpmnPool pool)
        {
            if (pool.Lanes == null || !pool.Lanes.Any(l => l.Bounds.Width > 0))
                return Math.Min(30, Math.Max(pool.Bounds.Width / 5, 0));

            int laneLeft = pool.Lanes.Where(l => l.Bounds.Width > 0).Min(l => l.Bounds.X);
            return Math.Max(0, Math.Min(laneLeft - pool.Bounds.X, pool.Bounds.Width));
        }

        private static void DrawVerticalPoolLabel(Graphics g, string name, Rectangle poolRect, int labelWidth)
        {
            using (var font = new Font("Segoe UI", 9, FontStyle.Regular))
            using (var brush = new SolidBrush(Color.Black))
            {
                var state = g.Save();
                float cx = poolRect.X + labelWidth / 2f;
                float cy = poolRect.Y + poolRect.Height / 2f;
                g.TranslateTransform(cx, cy);
                g.RotateTransform(-90);
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(name, font, brush, 0, 0, sf);
                }
                g.Restore(state);
            }
        }

        private static void DrawHorizontalLaneLabel(Graphics g, BpmnLane lane, int poolLabelWidth)
        {
            using (var font = new Font("Segoe UI", 8, FontStyle.Regular))
            using (var brush = new SolidBrush(Color.Black))
            {
                var labelRect = new RectangleF(
                    lane.Bounds.X + 4,
                    lane.Bounds.Y + 4,
                    Math.Min(80, poolLabelWidth > 0 ? poolLabelWidth - 4 : 80),
                    16);
                g.DrawString(lane.Name, font, brush, labelRect);
            }
        }

        private GraphicsPath GetRoundedRectanglePath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
            path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
            path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }
        private Color GetLaneColor(BpmnLane lane)
        {
            int laneIndex = _lanes.IndexOf(lane);
            Color[] colors = new Color[]
            {
        Color.FromArgb(255, 250, 250),  
        Color.FromArgb(248, 248, 255),  
        Color.FromArgb(245, 245, 245), 
        Color.FromArgb(250, 240, 230)  
            };

            return colors[laneIndex % colors.Length];
        }

        private void CalculateNodePositionsImproved()
        {
            nodePositions.Clear();
            nodeRectangles.Clear();

            if (_currentProcess == null || _currentProcess.Nodes.Count == 0) return;

            var startNode = _currentProcess.Nodes.Values.FirstOrDefault(n => n.Type == NodeType.StartEvent);
            if (startNode == null) return;

            var levels = new Dictionary<string, int>();
            var queue = new Queue<string>();
            levels[startNode.Id] = 0;
            queue.Enqueue(startNode.Id);

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                int currentLevel = levels[currentId];

                if (_currentProcess.OutgoingFlows.ContainsKey(currentId))
                {
                    foreach (var flow in _currentProcess.OutgoingFlows[currentId])
                    {
                        if (!levels.ContainsKey(flow.TargetRef))
                        {
                            levels[flow.TargetRef] = currentLevel + 1;
                            queue.Enqueue(flow.TargetRef);
                        }
                        else if (levels[flow.TargetRef] <= currentLevel)
                        {
                            levels[flow.TargetRef] = currentLevel + 1;
                        }
                    }
                }
            }

            var levelGroups = new Dictionary<int, List<BpmnNode>>();
            foreach (var node in _currentProcess.Nodes.Values)
            {
                int level = levels.ContainsKey(node.Id) ? levels[node.Id] : 0;
                if (!levelGroups.ContainsKey(level))
                    levelGroups[level] = new List<BpmnNode>();
                levelGroups[level].Add(node);
            }

            int startX = 100;
            int startY = 100;
            int horizontalSpacing = 220;
            int verticalSpacing = 120;

            int maxLevel = levelGroups.Keys.Max();
            _diagramWidth = startX + (maxLevel + 1) * horizontalSpacing + 200;

            foreach (var levelGroup in levelGroups.OrderBy(g => g.Key))
            {
                int x = startX + levelGroup.Key * horizontalSpacing;
                int nodesCount = levelGroup.Value.Count;

                int totalHeight = (nodesCount - 1) * verticalSpacing;
                int startYPos = startY + Math.Max(0, (600 - totalHeight) / 2);
                int currentY = startYPos;

                foreach (var node in levelGroup.Value.OrderBy(n => n.Id))
                {
                    nodePositions[node.Id] = new Point(x, currentY);

                    Rectangle rect;
                    if (node.Type == NodeType.StartEvent || node.Type == NodeType.EndEvent)
                    {
                        int size = 50; 
                        rect = new Rectangle(x - size / 2, currentY - size / 2, size, size);
                    }
                    else if (IsGatewayNode(node.Type))
                    {
                        int size = 50;
                        rect = new Rectangle(x - size / 2, currentY - size / 2, size, size);
                    }
                    else
                    {
                        rect = new Rectangle(x - 60, currentY - 30, 120, 60);
                    }

                    nodeRectangles[node.Id] = rect;
                    currentY += verticalSpacing;
                }
            }

            AdjustParallelGatewayPositions();

            _diagramHeight = startY + levelGroups.Count * verticalSpacing + 200;
        }

        private void CalculateNodePositionsUsingPools()
        {
            nodePositions.Clear();
            nodeRectangles.Clear();

            if (_pools == null || !_pools.Any())
            {
                CalculateDefaultNodePositions();
                return;
            }

            int horizontalSpacing = 220;
            int verticalSpacing = 100;
            int startXOffset = 120; 

            UpdatePoolBounds();

            foreach (var pool in _pools)
            {
                if (pool.Lanes == null || !pool.Lanes.Any())
                {
                    PlaceNodesInPool(pool, startXOffset, horizontalSpacing, verticalSpacing);
                    continue;
                }

                foreach (var lane in pool.Lanes.OrderBy(l => l.Order))
                {
                    var laneNodes = _currentProcess.Nodes.Values
                        .Where(n => n.LaneId == lane.Id)
                        .ToList();

                    if (!laneNodes.Any()) continue;

                    var levels = CalculateLevels(laneNodes);

                    int laneStartX = lane.Bounds.X + startXOffset;
                    int laneStartY = lane.Bounds.Y + 50;
                    int laneWidth = lane.Bounds.Width - startXOffset - 20;
                    int laneHeight = lane.Bounds.Height - 60;

                    int maxLevel = Math.Max(levels.Keys.DefaultIfEmpty(0).Max(), 0);
                    int availableWidth = Math.Max(laneWidth - 50, 200);
                    int stepX = availableWidth / Math.Max(maxLevel + 1, 1);

                    foreach (var levelGroup in levels.OrderBy(g => g.Key))
                    {
                        int x = laneStartX + (levelGroup.Key * stepX);
                        int nodesInLevel = levelGroup.Value.Count;

                        int availableHeight = laneHeight - 50;
                        int stepY = availableHeight / Math.Max(nodesInLevel, 1);
                        int startY = laneStartY + 25;

                        for (int i = 0; i < levelGroup.Value.Count; i++)
                        {
                            var node = levelGroup.Value[i];
                            int y = startY + (i * stepY);

                            y = Math.Max(laneStartY + 10, Math.Min(y, lane.Bounds.Bottom - 70));

                            nodePositions[node.Id] = new Point(x, y);

                            Rectangle rect;
                            if (node.Type == NodeType.StartEvent || node.Type == NodeType.EndEvent)
                            {
                                int size = 50;
                                rect = new Rectangle(x - size / 2, y - size / 2, size, size);
                            }
                            else if (IsGatewayNode(node.Type))
                            {
                                int size = 50;
                                rect = new Rectangle(x - size / 2, y - size / 2, size, size);
                            }
                            else
                            {
                                rect = new Rectangle(x - 60, y - 30, 120, 60);
                            }

                            nodeRectangles[node.Id] = rect;
                        }
                    }
                }
            }

            if (_pools.Any())
            {
                _diagramWidth = _pools.Max(p => p.Bounds.Right) + 100;
                _diagramHeight = _pools.Max(p => p.Bounds.Bottom) + 100;
            }
        }

        private void UpdatePoolBounds()
        {
            foreach (var pool in _pools)
            {
                var poolNodes = _currentProcess.Nodes.Values
                    .Where(n => n.PoolId == pool.Id)
                    .ToList();

                if (poolNodes.Any())
                {
                    var minX = pool.Bounds.X;
                    var minY = pool.Bounds.Y;
                    var maxX = pool.Bounds.Right;
                    var maxY = pool.Bounds.Bottom;

                    if (pool.Lanes.Any())
                    {
                        minX = Math.Min(minX, pool.Lanes.Min(l => l.Bounds.X));
                        minY = Math.Min(minY, pool.Lanes.Min(l => l.Bounds.Y));
                        maxX = Math.Max(maxX, pool.Lanes.Max(l => l.Bounds.Right));
                        maxY = Math.Max(maxY, pool.Lanes.Max(l => l.Bounds.Bottom));
                    }

                    pool.Bounds = new Rectangle(minX, minY, maxX - minX, maxY - minY);
                }
            }
        }

        private void PlaceNodesInPool(BpmnPool pool, int startXOffset, int horizontalSpacing, int verticalSpacing)
        {
            var poolNodes = _currentProcess.Nodes.Values
                .Where(n => n.PoolId == pool.Id)
                .ToList();

            if (!poolNodes.Any()) return;

            var levels = CalculateLevels(poolNodes);
            int poolStartX = pool.Bounds.X + startXOffset;
            int poolStartY = pool.Bounds.Y + 40;

            foreach (var levelGroup in levels.OrderBy(g => g.Key))
            {
                int x = poolStartX + levelGroup.Key * horizontalSpacing;
                int nodesCount = levelGroup.Value.Count;
                int totalHeight = (nodesCount - 1) * verticalSpacing;
                int startYPos = poolStartY + Math.Max(0, (pool.Bounds.Height - 80 - totalHeight) / 2);
                int currentY = startYPos;

                foreach (var node in levelGroup.Value.OrderBy(n => n.Id))
                {
                    nodePositions[node.Id] = new Point(x, currentY);

                    Rectangle rect;
                    if (node.Type == NodeType.StartEvent || node.Type == NodeType.EndEvent)
                    {
                        int size = 50;
                        rect = new Rectangle(x - size / 2, currentY - size / 2, size, size);
                    }
                    else if (IsGatewayNode(node.Type))
                    {
                        int size = 50;
                        rect = new Rectangle(x - size / 2, currentY - size / 2, size, size);
                    }
                    else
                    {
                        rect = new Rectangle(x - 60, currentY - 30, 120, 60);
                    }

                    nodeRectangles[node.Id] = rect;
                    currentY += verticalSpacing;
                }
            }
        }

        private void CalculateDefaultNodePositions()
        {

            nodePositions.Clear();
            nodeRectangles.Clear();

            if (_currentProcess == null || _currentProcess.Nodes.Count == 0) return;

            var startNode = _currentProcess.Nodes.Values.FirstOrDefault(n => n.Type == NodeType.StartEvent);
            if (startNode == null) return;

            var levels = new Dictionary<string, int>();
            var queue = new Queue<string>();
            levels[startNode.Id] = 0;
            queue.Enqueue(startNode.Id);

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                int currentLevel = levels[currentId];

                if (_currentProcess.OutgoingFlows.ContainsKey(currentId))
                {
                    foreach (var flow in _currentProcess.OutgoingFlows[currentId])
                    {
                        if (!levels.ContainsKey(flow.TargetRef))
                        {
                            levels[flow.TargetRef] = currentLevel + 1;
                            queue.Enqueue(flow.TargetRef);
                        }
                        else if (levels[flow.TargetRef] <= currentLevel)
                        {
                            levels[flow.TargetRef] = currentLevel + 1;
                        }
                    }
                }
            }

            var levelGroups = new Dictionary<int, List<BpmnNode>>();
            foreach (var node in _currentProcess.Nodes.Values)
            {
                int level = levels.ContainsKey(node.Id) ? levels[node.Id] : 0;
                if (!levelGroups.ContainsKey(level))
                    levelGroups[level] = new List<BpmnNode>();
                levelGroups[level].Add(node);
            }

            int startX = 100;
            int startY = 100;
            int horizontalSpacing = 220;
            int verticalSpacing = 120;

            int maxLevel = levelGroups.Keys.Max();
            _diagramWidth = startX + (maxLevel + 1) * horizontalSpacing + 200;

            foreach (var levelGroup in levelGroups.OrderBy(g => g.Key))
            {
                int x = startX + levelGroup.Key * horizontalSpacing;
                int nodesCount = levelGroup.Value.Count;

                int totalHeight = (nodesCount - 1) * verticalSpacing;
                int startYPos = startY + Math.Max(0, (600 - totalHeight) / 2);
                int currentY = startYPos;

                foreach (var node in levelGroup.Value.OrderBy(n => n.Id))
                {
                    nodePositions[node.Id] = new Point(x, currentY);

                    Rectangle rect;
                    if (node.Type == NodeType.StartEvent || node.Type == NodeType.EndEvent)
                    {
                        int size = 50;
                        rect = new Rectangle(x - size / 2, currentY - size / 2, size, size);
                    }
                    else if (IsGatewayNode(node.Type))
                    {
                        int size = 50;
                        rect = new Rectangle(x - size / 2, currentY - size / 2, size, size);
                    }
                    else
                    {
                        rect = new Rectangle(x - 60, currentY - 30, 120, 60);
                    }

                    nodeRectangles[node.Id] = rect;
                    currentY += verticalSpacing;
                }
            }

            AdjustParallelGatewayPositions();

            _diagramHeight = startY + levelGroups.Count * verticalSpacing + 200;
        }

        private Dictionary<int, List<BpmnNode>> CalculateLevels(List<BpmnNode> nodes)
        {
            var levels = new Dictionary<string, int>();
            var queue = new Queue<string>();

            var startNode = nodes.FirstOrDefault(n => n.Type == NodeType.StartEvent);
            if (startNode == null && nodes.Any())
                startNode = nodes.First();

            if (startNode != null)
            {
                levels[startNode.Id] = 0;
                queue.Enqueue(startNode.Id);
            }

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                int currentLevel = levels[currentId];

                if (_currentProcess.OutgoingFlows.ContainsKey(currentId))
                {
                    foreach (var flow in _currentProcess.OutgoingFlows[currentId])
                    {
                        var targetNode = _currentProcess.Nodes.ContainsKey(flow.TargetRef)
                        ? _currentProcess.Nodes[flow.TargetRef]
                        : null;
                        if (targetNode != null && nodes.Contains(targetNode))
                        {
                            if (!levels.ContainsKey(flow.TargetRef))
                            {
                                levels[flow.TargetRef] = currentLevel + 1;
                                queue.Enqueue(flow.TargetRef);
                            }
                        }
                    }
                }
            }

            var levelGroups = new Dictionary<int, List<BpmnNode>>();
            foreach (var node in nodes)
            {
                int level = levels.ContainsKey(node.Id) ? levels[node.Id] : 0;
                if (!levelGroups.ContainsKey(level))
                    levelGroups[level] = new List<BpmnNode>();
                levelGroups[level].Add(node);
            }

            return levelGroups;
        }

        private void AdjustParallelGatewayPositions()
        {
            var parallelGateways = _currentProcess.Nodes.Values
                .Where(n => n.Type == NodeType.ParallelGateway)
                .ToList();

            foreach (var gateway in parallelGateways)
            {
                if (!nodePositions.ContainsKey(gateway.Id)) continue;

                var incomingFlows = _currentProcess.OutgoingFlows
                    .SelectMany(kvp => kvp.Value)
                    .Where(f => f.TargetRef == gateway.Id)
                    .ToList();

                var outgoingFlows = _currentProcess.OutgoingFlows.ContainsKey(gateway.Id)
                    ? _currentProcess.OutgoingFlows[gateway.Id]
                    : new List<SequenceFlow>();

                Point gatewayPos = nodePositions[gateway.Id];
                int avgY = gatewayPos.Y;
                int count = 1;

                foreach (var flow in incomingFlows)
                {
                    if (nodePositions.ContainsKey(flow.SourceRef))
                    {
                        avgY += nodePositions[flow.SourceRef].Y;
                        count++;
                    }
                }

                foreach (var flow in outgoingFlows)
                {
                    if (nodePositions.ContainsKey(flow.TargetRef))
                    {
                        avgY += nodePositions[flow.TargetRef].Y;
                        count++;
                    }
                }

                if (count > 1)
                {
                    int newY = avgY / count;
                    nodePositions[gateway.Id] = new Point(gatewayPos.X, newY);
                    nodeRectangles[gateway.Id] = new Rectangle(gatewayPos.X - 60, newY - 30, 120, 60);
                }
            }
        }

        private void DrawEmptyStateMessage(Graphics g)
        {
            using (Font font = new Font("Segoe UI", 16))
            using (Brush brush = new SolidBrush(Color.Gray))
            {
                string message = "📁 Загрузите BPMN файл для отображения диаграммы\n\n" +
                               "Нажмите кнопку 'Загрузить BPMN' в верхней панели";
                g.DrawString(message, font, brush, new PointF(100, 100));
            }
        }
        private void DrawGrid(Graphics g)
        {
            int step = 50;

            using (Pen pen = new Pen(Color.FromArgb(220, 220, 220), 1))
            {
                pen.DashStyle = DashStyle.Dash;

                for (int x = 0; x < _diagramWidth; x += step)
                {
                    g.DrawLine(pen, x, 0, x, _diagramHeight);
                }

                for (int y = 0; y < _diagramHeight; y += step)
                {
                    g.DrawLine(pen, 0, y, _diagramWidth, y);
                }
            }
        }

        private void DrawNodeBpmnStandard(Graphics g, BpmnNode node, Rectangle rect)
        {
            Color borderColor = Color.Black;
            int borderWidth = 2;
            Color fillColor = GetNodeColor(node);

            switch (node.Type)
            {
                case NodeType.StartEvent:
                    using (Brush brush = new SolidBrush(fillColor))
                    using (Pen pen = new Pen(borderColor, borderWidth))
                    {
                        g.FillEllipse(brush, rect);
                        g.DrawEllipse(pen, rect);
                    }
                    break;

                case NodeType.EndEvent:
                    using (Brush brush = new SolidBrush(fillColor))
                    using (Pen pen = new Pen(borderColor, borderWidth))
                    {
                        g.FillEllipse(brush, rect);
                        g.DrawEllipse(pen, rect);
                        using (Pen thickPen = new Pen(borderColor, borderWidth * 2))
                        {
                            int padding = rect.Width / 5;
                            Rectangle innerRect = new Rectangle(rect.X + padding, rect.Y + padding,
                                                                rect.Width - padding * 2, rect.Height - padding * 2);
                            g.DrawEllipse(thickPen, innerRect);
                        }
                    }
                    break;

                case NodeType.Task:
                    {
                        var taskNode = node as Task;
                        int taskBorder = BpmnTaskIconRenderer.GetTaskBorderWidth(
                            taskNode?.TaskKind ?? BpmnTaskKind.Generic, borderWidth);
                        using (Brush brush = new SolidBrush(fillColor))
                        using (Pen pen = new Pen(borderColor, taskBorder))
                        {
                            GraphicsPath path = GetRoundedRectangle(rect, 8);
                            g.FillPath(brush, path);
                            g.DrawPath(pen, path);
                            if (taskNode != null)
                                BpmnTaskIconRenderer.Draw(g, rect, taskNode);
                        }
                    }
                    break;

                case NodeType.ExclusiveGateway:
                    DrawDiamond(g, rect, fillColor, borderColor, borderWidth);
                    using (Pen pen = new Pen(borderColor, borderWidth))
                    {
                        // Рисуем X внутри ромба
                        int centerX = rect.X + rect.Width / 2;
                        int centerY = rect.Y + rect.Height / 2;
                        int size = Math.Min(GATEWAY_SIZE, Math.Min(rect.Width, rect.Height) / 2);
                        int offset = size / 2;

                        g.DrawLine(pen, centerX - offset, centerY - offset, centerX + offset, centerY + offset);
                        g.DrawLine(pen, centerX + offset, centerY - offset, centerX - offset, centerY + offset);
                    }
                    break;

                case NodeType.ParallelGateway:
                    DrawDiamond(g, rect, fillColor, borderColor, borderWidth);
                    using (Pen penPlus = new Pen(borderColor, borderWidth))
                    {
                        int centerX = rect.X + rect.Width / 2;
                        int centerY = rect.Y + rect.Height / 2;
                        int size = Math.Min(GATEWAY_SIZE, Math.Min(rect.Width, rect.Height) / 2);
                        int offset = size / 2;

                        g.DrawLine(penPlus, centerX - offset, centerY, centerX + offset, centerY);
                        g.DrawLine(penPlus, centerX, centerY - offset, centerX, centerY + offset);
                    }
                    break;

                case NodeType.InclusiveGateway:
                    DrawDiamond(g, rect, fillColor, borderColor, borderWidth);
                    using (Pen penO = new Pen(borderColor, borderWidth))
                    {
                        int centerX = rect.X + rect.Width / 2;
                        int centerY = rect.Y + rect.Height / 2;
                        int radius = Math.Min(GATEWAY_SIZE / 2, Math.Min(rect.Width, rect.Height) / 4);
                        g.DrawEllipse(penO, centerX - radius, centerY - radius, radius * 2, radius * 2);
                    }
                    break;

                default:
                    using (Brush brush = new SolidBrush(fillColor))
                    using (Pen pen = new Pen(borderColor, borderWidth))
                    {
                        g.FillRectangle(brush, rect);
                        g.DrawRectangle(pen, rect);
                    }
                    break;
            }

            string displayName = string.IsNullOrEmpty(node.Name) ? GetDefaultNodeName(node.Type) : node.Name;
            if (displayName.Length > 20) displayName = displayName.Substring(0, 17) + "...";

            if (!IsGatewayNode(node.Type))
            {
                using (Font font = new Font("Segoe UI", 8, FontStyle.Regular))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    Rectangle textRect;
                    if (node.Type == NodeType.StartEvent || node.Type == NodeType.EndEvent)
                    {
                        textRect = new Rectangle(rect.X - 20, rect.Bottom + 2, rect.Width + 40, 20);
                    }
                    else
                    {
                        textRect = new Rectangle(rect.X - 20, rect.Bottom + 2, rect.Width + 40, 20);
                    }
                    g.DrawString(displayName, font, Brushes.Black, textRect, sf);
                }
            }

            DrawGatewayControls(g, node, rect);
        }

        private void DrawGatewayControls(Graphics g, BpmnNode node, Rectangle rect)
        {
            if (!IsGatewayNode(node.Type) || !ShouldShowGatewayFlowButtons(node.Id))
                return;

            var outgoingFlows = GetOutgoingFlows(node.Id);
            int buttonSize = 22;
            int buttonX = rect.Right + 6;
            int startY = rect.Y + rect.Height / 2 - (outgoingFlows.Count * (buttonSize + 2) - 2) / 2;

            var buttons = new List<GatewayFlowButton>();

            for (int i = 0; i < outgoingFlows.Count; i++)
            {
                var flow = outgoingFlows[i];
                var buttonRect = new Rectangle(buttonX, startY + i * (buttonSize + 2), buttonSize, buttonSize);
                bool isSelected = IsGatewayFlowSelected(node, flow.Id);

                Color fill = isSelected ? Color.FromArgb(220, 70, 130, 180) : Color.FromArgb(230, 230, 230);
                using (Brush brush = new SolidBrush(fill))
                using (Pen pen = new Pen(isSelected ? Color.DarkBlue : Color.Black, isSelected ? 2 : 1))
                using (Font iconFont = new Font("Segoe UI", 9, FontStyle.Bold))
                {
                    g.FillRectangle(brush, buttonRect);
                    g.DrawRectangle(pen, buttonRect);
                    g.DrawString((i + 1).ToString(), iconFont,
                        isSelected ? Brushes.White : Brushes.Black,
                        buttonRect.X + 6, buttonRect.Y + 4);
                }

                buttons.Add(new GatewayFlowButton { FlowId = flow.Id, Bounds = buttonRect });
            }

            _gatewayFlowButtons[node.Id] = buttons;
        }

        private static bool IsGatewayNode(NodeType type)
        {
            return type == NodeType.ExclusiveGateway
                || type == NodeType.ParallelGateway
                || type == NodeType.InclusiveGateway;
        }

        private bool ShouldShowGatewayFlowButtons(string gatewayId)
        {
            var outgoing = GetOutgoingFlows(gatewayId);
            if (outgoing.Count <= 1)
                return false;

            int incomingCount = CountIncomingFlows(gatewayId);
            return !(incomingCount > 1 && outgoing.Count == 1);
        }

        private List<SequenceFlow> GetOutgoingFlows(string nodeId)
        {
            return _currentProcess.OutgoingFlows.ContainsKey(nodeId)
                ? _currentProcess.OutgoingFlows[nodeId]
                : new List<SequenceFlow>();
        }

        private int CountIncomingFlows(string nodeId)
        {
            return _currentProcess.OutgoingFlows.Values
                .SelectMany(f => f)
                .Count(f => f.TargetRef == nodeId);
        }

        private bool IsGatewayFlowSelected(BpmnNode gateway, string flowId)
        {
            switch (gateway.Type)
            {
                case NodeType.ExclusiveGateway:
                case NodeType.ParallelGateway:
                    return _gatewaySelectedFlows.TryGetValue(gateway.Id, out var selected) && selected == flowId;
                case NodeType.InclusiveGateway:
                    return _gatewayInclusiveSelectedFlows.TryGetValue(gateway.Id, out var set) && set.Contains(flowId);
                default:
                    return false;
            }
        }

        private void SyncSimulatorGatewaySelections()
        {
            if (_simulator == null)
                return;

            _simulator.UpdateUserSelections(_gatewaySelectedFlows, _gatewayInclusiveSelectedFlows);
        }

        private Color GetNodeColor(BpmnNode node)
        {
            switch (node.Type)
            {
                case NodeType.StartEvent: return Color.FromArgb(144, 238, 144);
                case NodeType.EndEvent: return Color.FromArgb(240, 128, 128);
                case NodeType.Task: return Color.FromArgb(173, 216, 230);
                case NodeType.ExclusiveGateway: return Color.FromArgb(255, 255, 224);
                case NodeType.ParallelGateway: return Color.FromArgb(176, 196, 222);
                case NodeType.InclusiveGateway: return Color.FromArgb(255, 250, 205);
                default: return Color.LightGray;
            }
        }

        private string GetDefaultNodeName(NodeType type)
        {
            switch (type)
            {
                case NodeType.StartEvent: return "Старт";
                case NodeType.EndEvent: return "Конец";
                case NodeType.Task: return "Задача";
                case NodeType.ExclusiveGateway: return "XOR";
                case NodeType.ParallelGateway: return "AND";
                case NodeType.InclusiveGateway: return "OR";
                default: return type.ToString();
            }
        }

        private GraphicsPath GetRoundedRectangle(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
            path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
            path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void DrawDiamond(Graphics g, Rectangle rect, Color fillColor, Color borderColor, int borderWidth)
        {
            int centerX = rect.X + rect.Width / 2;
            int centerY = rect.Y + rect.Height / 2;
            int maxSize = Math.Min(rect.Width, rect.Height) / 2;
            int actualSize = Math.Min(GATEWAY_SIZE, maxSize);

            Point[] diamond = new Point[]
            {
        new Point(centerX, centerY - actualSize), // Верхняя вершина
        new Point(centerX + actualSize, centerY), // Правая вершина
        new Point(centerX, centerY + actualSize), // Нижняя вершина
        new Point(centerX - actualSize, centerY)  // Левая вершина
            };

            using (Brush brush = new SolidBrush(fillColor))
            using (Pen pen = new Pen(borderColor, borderWidth))
            {
                g.FillPolygon(brush, diamond);
                g.DrawPolygon(pen, diamond);
            }
        }

        private Point GetConnectionPoint(Rectangle rect, BpmnNode node, bool isOutput)
        {
            Point center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);

            // Для ромбовидных узлов (шлюзов)
            if (IsGatewayNode(node.Type))
            {
                int centerX = rect.X + rect.Width / 2;
                int centerY = rect.Y + rect.Height / 2;

                int maxSize = Math.Min(rect.Width, rect.Height) / 2;
                int actualSize = Math.Min(GATEWAY_SIZE, maxSize);

                if (isOutput)
                {
                    return new Point(centerX + actualSize, centerY);
                }
                else
                {
                    return new Point(centerX - actualSize, centerY);
                }
            }
            else if (node.Type == NodeType.StartEvent || node.Type == NodeType.EndEvent)
            {
                double angle = isOutput ? 0 : Math.PI;
                int radius = rect.Width / 2;
                int centerX = rect.X + radius;
                int centerY = rect.Y + radius;

                int x = centerX + (int)(radius * Math.Cos(angle));
                int y = centerY + (int)(radius * Math.Sin(angle));

                return new Point(x, y);
            }
            else
            {
                if (isOutput)
                {
                    return new Point(rect.Right, center.Y);
                }
                else
                {
                    return new Point(rect.Left, center.Y);
                }
            }
        }
        private void CalculateFlowPaths()
        {
            flowPaths.Clear();

            foreach (var kvp in _currentProcess.OutgoingFlows)
            {
                string sourceId = kvp.Key;
                if (!nodeRectangles.ContainsKey(sourceId) || !_currentProcess.Nodes.ContainsKey(sourceId))
                    continue;

                Rectangle sourceRect = nodeRectangles[sourceId];
                BpmnNode sourceNode = _currentProcess.Nodes[sourceId];
                Point sourcePoint = GetConnectionPoint(sourceRect, sourceNode, true);

                foreach (var flow in kvp.Value)
                {
                    if (!nodeRectangles.ContainsKey(flow.TargetRef) || !_currentProcess.Nodes.ContainsKey(flow.TargetRef))
                        continue;

                    Rectangle targetRect = nodeRectangles[flow.TargetRef];
                    BpmnNode targetNode = _currentProcess.Nodes[flow.TargetRef];
                    Point targetPoint = GetConnectionPoint(targetRect, targetNode, false);

                    var path = new List<Point>();

                    bool isSourceGateway = IsGatewayNode(sourceNode.Type);
                    bool isTargetGateway = IsGatewayNode(targetNode.Type);

                    // 1. ИСХОДЯЩИЙ ПОТОК ИЗ ШЛЮЗА
                    if (isSourceGateway && !isTargetGateway)
                    {
                        int centerX = sourceRect.X + sourceRect.Width / 2;
                        int centerY = sourceRect.Y + sourceRect.Height / 2;

                        int maxSize = Math.Min(sourceRect.Width, sourceRect.Height) / 2;
                        int actualSize = Math.Min(GATEWAY_SIZE, maxSize);

                        Point topVertex = new Point(centerX, centerY - actualSize);
                        Point bottomVertex = new Point(centerX, centerY + actualSize);
                        Point rightVertex = new Point(centerX + actualSize, centerY);
                        Point leftVertex = new Point(centerX - actualSize, centerY);

                        int targetCenterY = targetRect.Y + targetRect.Height / 2;
                        int targetCenterX = targetRect.X + targetRect.Width / 2;
                        int sourceCenterY = centerY;

                        int verticalDistance = CalculateAutomaticDistance(sourceRect, targetRect, sourceCenterY, targetCenterY);

                        if (targetCenterY < sourceCenterY - 15)
                        {
                            Point exitPoint = topVertex;
                            int actualDistance = Math.Min(Math.Max(verticalDistance, 30), 100);
                            Point verticalPoint = new Point(exitPoint.X, exitPoint.Y - actualDistance);
                            Point horizontalPoint = new Point(targetPoint.X, verticalPoint.Y);

                            path.Add(exitPoint);
                            path.Add(verticalPoint);
                            path.Add(horizontalPoint);
                            path.Add(targetPoint);
                        }
                        else if (targetCenterY > sourceCenterY + 15)
                        {
                            Point exitPoint = bottomVertex;
                            int actualDistance = Math.Min(Math.Max(verticalDistance, 30), 100);
                            Point verticalPoint = new Point(exitPoint.X, exitPoint.Y + actualDistance);
                            Point horizontalPoint = new Point(targetPoint.X, verticalPoint.Y);

                            path.Add(exitPoint);
                            path.Add(verticalPoint);
                            path.Add(horizontalPoint);
                            path.Add(targetPoint);
                        }
                        else
                        {
                            if (targetCenterX > centerX)
                            {
                                path.Add(rightVertex);
                                path.Add(targetPoint);
                            }
                            else
                            {
                                Point exitPoint = leftVertex;
                                Point horizontalPoint = new Point(targetPoint.X, exitPoint.Y);
                                path.Add(exitPoint);
                                path.Add(horizontalPoint);
                                path.Add(targetPoint);
                            }
                        }
                    }
                    // 2. ВХОДЯЩИЙ ПОТОК В ШЛЮЗ
                    else if (!isSourceGateway && isTargetGateway)
                    {
                        int centerX = targetRect.X + targetRect.Width / 2;
                        int centerY = targetRect.Y + targetRect.Height / 2;

                        int maxSize = Math.Min(targetRect.Width, targetRect.Height) / 2;
                        int actualSize = Math.Min(GATEWAY_SIZE, maxSize);

                        Point topVertex = new Point(centerX, centerY - actualSize);
                        Point bottomVertex = new Point(centerX, centerY + actualSize);
                        Point rightVertex = new Point(centerX + actualSize, centerY);
                        Point leftVertex = new Point(centerX - actualSize, centerY);

                        int sourceCenterY = sourceRect.Y + sourceRect.Height / 2;
                        int sourceCenterX = sourceRect.X + sourceRect.Width / 2;
                        int targetCenterY = centerY;

                        int verticalDistance = CalculateAutomaticDistance(sourceRect, targetRect, sourceCenterY, targetCenterY);

                        if (sourceCenterY < targetCenterY - 15)
                        {
                            Point entryPoint = topVertex;
                            int actualDistance = Math.Min(Math.Max(verticalDistance, 30), 100);
                            Point verticalPoint = new Point(entryPoint.X, entryPoint.Y - actualDistance);
                            Point horizontalPoint = new Point(sourcePoint.X, verticalPoint.Y);

                            path.Add(sourcePoint);
                            path.Add(horizontalPoint);
                            path.Add(verticalPoint);
                            path.Add(entryPoint);
                        }
                        else if (sourceCenterY > targetCenterY + 15)
                        {
                            Point entryPoint = bottomVertex;
                            int actualDistance = Math.Min(Math.Max(verticalDistance, 30), 100);
                            Point verticalPoint = new Point(entryPoint.X, entryPoint.Y + actualDistance);
                            Point horizontalPoint = new Point(sourcePoint.X, verticalPoint.Y);

                            path.Add(sourcePoint);
                            path.Add(horizontalPoint);
                            path.Add(verticalPoint);
                            path.Add(entryPoint);
                        }
                        else
                        {
                            if (sourceCenterX < centerX)
                            {
                                path.Add(sourcePoint);
                                path.Add(leftVertex);
                            }
                            else
                            {
                                path.Add(sourcePoint);
                                path.Add(rightVertex);
                            }
                        }
                    }
                    // 3. ПОТОК МЕЖДУ ДВУМЯ ШЛЮЗАМИ
                    else if (isSourceGateway && isTargetGateway)
                    {
                        int sourceCenterX = sourceRect.X + sourceRect.Width / 2;
                        int sourceCenterY = sourceRect.Y + sourceRect.Height / 2;
                        int targetCenterX = targetRect.X + targetRect.Width / 2;
                        int targetCenterY = targetRect.Y + targetRect.Height / 2;

                        int sourceMaxSize = Math.Min(sourceRect.Width, sourceRect.Height) / 2;
                        int sourceActualSize = Math.Min(GATEWAY_SIZE, sourceMaxSize);
                        int targetMaxSize = Math.Min(targetRect.Width, targetRect.Height) / 2;
                        int targetActualSize = Math.Min(GATEWAY_SIZE, targetMaxSize);

                        Point sourceRightVertex = new Point(sourceCenterX + sourceActualSize, sourceCenterY);
                        Point targetLeftVertex = new Point(targetCenterX - targetActualSize, targetCenterY);

                        if (Math.Abs(sourceCenterY - targetCenterY) < 30)
                        {
                            path.Add(sourceRightVertex);
                            path.Add(targetLeftVertex);
                        }
                        else
                        {
                            int midY = (sourceCenterY + targetCenterY) / 2;
                            path.Add(sourceRightVertex);
                            path.Add(new Point(sourceRightVertex.X, midY));
                            path.Add(new Point(targetLeftVertex.X, midY));
                            path.Add(targetLeftVertex);
                        }
                    }
                    // 4. СТАНДАРТНЫЙ ПОТОК
                    else
                    {
                        int midX = (sourcePoint.X + targetPoint.X) / 2;
                        int deltaY = targetPoint.Y - sourcePoint.Y;

                        if (Math.Abs(deltaY) < 20)
                        {
                            path.Add(sourcePoint);
                            path.Add(targetPoint);
                        }
                        else
                        {
                            path.Add(sourcePoint);
                            path.Add(new Point(midX, sourcePoint.Y));
                            path.Add(new Point(midX, targetPoint.Y));
                            path.Add(targetPoint);
                        }
                    }

                    if (path.Count >= 2)
                    {
                        flowPaths[flow.Id] = path;
                    }
                }
            }
        }

        private int CalculateAutomaticDistance(Rectangle sourceRect, Rectangle targetRect, int sourceCenterY, int targetCenterY)
        {
            int verticalGap = Math.Abs(targetCenterY - sourceCenterY);

            int edgeDistance;
            if (targetCenterY < sourceCenterY)
            {
                edgeDistance = sourceRect.Top - targetRect.Bottom;
            }
            else
            {
                edgeDistance = targetRect.Top - sourceRect.Bottom;
            }

            int calculatedDistance = Math.Max(30, verticalGap / 3);

            if (edgeDistance > 0 && edgeDistance < 50)
            {
                calculatedDistance = Math.Max(20, calculatedDistance - 10);
            }
            else if (edgeDistance > 150)
            {
                calculatedDistance = Math.Min(100, calculatedDistance + 15);
            }

            calculatedDistance = Math.Min(100, Math.Max(20, calculatedDistance));

            return calculatedDistance;
        }


        private struct GatewayVertices
        {
            public Point Top { get; set; }
            public Point Bottom { get; set; }
            public Point Left { get; set; }
            public Point Right { get; set; }
        }

        private GatewayVertices GetGatewayVertices(Rectangle rect)
        {
            int centerX = rect.X + rect.Width / 2;
            int centerY = rect.Y + rect.Height / 2;

            int maxSize = Math.Min(rect.Width, rect.Height) / 2;
            int actualSize = Math.Min(GATEWAY_SIZE, maxSize);

            return new GatewayVertices
            {
                Top = new Point(centerX, centerY - actualSize),
                Bottom = new Point(centerX, centerY + actualSize),
                Left = new Point(centerX - actualSize, centerY),
                Right = new Point(centerX + actualSize, centerY)
            };
        }

        private Point GetGatewayExitPoint(GatewayVertices vertices, Point targetPoint)
        {
            double angleToTarget = Math.Atan2(targetPoint.Y - vertices.Top.Y, targetPoint.X - vertices.Top.X);
            double angleDegrees = angleToTarget * 180 / Math.PI;

            if (angleDegrees >= -45 && angleDegrees <= 45)
                return vertices.Right;  // Цель справа
            else if (angleDegrees > 45 && angleDegrees <= 135)
                return vertices.Bottom; // Цель снизу
            else if (angleDegrees >= -135 && angleDegrees < -45)
                return vertices.Top;    // Цель сверху
            else
                return vertices.Left;   // Цель слева
        }

        private Point GetGatewayEntryPoint(GatewayVertices vertices, Point sourcePoint)
        {
            double angleToSource = Math.Atan2(sourcePoint.Y - vertices.Top.Y, sourcePoint.X - vertices.Top.X);
            double angleDegrees = angleToSource * 180 / Math.PI;

            if (angleDegrees >= -45 && angleDegrees <= 45)
                return vertices.Left;   // Источник слева
            else if (angleDegrees > 45 && angleDegrees <= 135)
                return vertices.Top;    // Источник сверху
            else if (angleDegrees >= -135 && angleDegrees < -45)
                return vertices.Bottom; // Источник снизу
            else
                return vertices.Right;  // Источник справа
        }

        private List<Point> CalculateTurnPoints(Point startPoint, Point endPoint, Rectangle targetRect, BpmnNode targetNode)
        {
            var points = new List<Point>();

            Point targetConnectionPoint = GetConnectionPoint(targetRect, targetNode, false);

            bool needVerticalBend = Math.Abs(startPoint.Y - targetConnectionPoint.Y) > 30;

            if (!needVerticalBend)
            {
                points.Add(targetConnectionPoint);
            }
            else
            {
                int distanceToTarget = Math.Abs(targetConnectionPoint.X - startPoint.X);
                int targetHeight = targetRect.Height;

                int turnDistance = Math.Min(distanceToTarget / 3, Math.Max(30, targetHeight / 2));

                Point horizontalTurn = new Point(startPoint.X + turnDistance, startPoint.Y);

                Point verticalTurn = new Point(horizontalTurn.X, targetConnectionPoint.Y);

                points.Add(horizontalTurn);
                points.Add(verticalTurn);
                points.Add(targetConnectionPoint);
            }

            return points;
        }

        private List<Point> CalculateTurnPointsReverse(Point startPoint, Point endPoint, Rectangle sourceRect, BpmnNode sourceNode)
        {
            var points = new List<Point>();

            Point sourceConnectionPoint = GetConnectionPoint(sourceRect, sourceNode, true);

            bool needVerticalBend = Math.Abs(sourceConnectionPoint.Y - endPoint.Y) > 30;

            if (!needVerticalBend)
            {
                points.Add(endPoint);
            }
            else
            {
                int distanceToTarget = Math.Abs(endPoint.X - sourceConnectionPoint.X);
                int sourceHeight = sourceRect.Height;

                int turnDistance = Math.Min(distanceToTarget / 3, Math.Max(30, sourceHeight / 2));

                Point horizontalTurn = new Point(endPoint.X - turnDistance, endPoint.Y);
                Point verticalTurn = new Point(horizontalTurn.X, sourceConnectionPoint.Y);

                points.Add(verticalTurn);
                points.Add(horizontalTurn);
                points.Add(endPoint);
            }

            return points;
        }

        private int CalculateOptimalOffset(Point sourcePoint, Point targetPoint, Rectangle targetRect, BpmnNode targetNode)
        {
            int distance = (int)Math.Sqrt(
                Math.Pow(targetPoint.X - sourcePoint.X, 2) +
                Math.Pow(targetPoint.Y - sourcePoint.Y, 2)
            );

            int dynamicOffset = Math.Max(20, Math.Min(60, distance / 5));

            switch (targetNode.Type)
            {
                case NodeType.StartEvent:
                case NodeType.EndEvent:
                    return dynamicOffset + 5;
                case NodeType.Task:
                    return dynamicOffset;
                case NodeType.ExclusiveGateway:
                case NodeType.ParallelGateway:
                case NodeType.InclusiveGateway:
                    return dynamicOffset - 5;
                default:
                    return dynamicOffset;
            }
        }

        private int CalculateDynamicOffset(Rectangle targetRect, BpmnNode targetNode)
        {

            int baseOffset = 40;

            switch (targetNode.Type)
            {
                case NodeType.StartEvent:
                case NodeType.EndEvent:
                    return baseOffset + 10;

                case NodeType.Task:
                    return baseOffset;

                case NodeType.ExclusiveGateway:
                case NodeType.ParallelGateway:
                case NodeType.InclusiveGateway:
                    return baseOffset - 10;

                default:
                    return baseOffset;
            }
        }

        private void DrawFlow(Graphics g, List<Point> points, string flowId)
        {
            if (points.Count < 2) return;

            Color flowColor = _useBpmnDiLayout ? Color.Black : Color.FromArgb(80, 80, 100);
            using (Pen pen = new Pen(flowColor, _useBpmnDiLayout ? 1 : 2))
            {
                pen.EndCap = LineCap.ArrowAnchor;
                pen.StartCap = LineCap.Flat;
                g.DrawLines(pen, points.ToArray());
            }

            var flow = _currentProcess.OutgoingFlows.Values.SelectMany(f => f).FirstOrDefault(f => f.Id == flowId);
            if (flow != null && _currentProcess.Nodes.ContainsKey(flow.SourceRef))
            {
                var sourceNode = _currentProcess.Nodes[flow.SourceRef];
                if (!ShouldShowGatewayFlowButtons(flow.SourceRef))
                    return;

                if (!nodeRectangles.ContainsKey(flow.SourceRef)) return;

                bool isSelected = IsGatewayFlowSelected(sourceNode, flow.Id);

                var outgoingFlows = _currentProcess.OutgoingFlows[flow.SourceRef];
                int flowIndex = outgoingFlows.FindIndex(f => f.Id == flow.Id) + 1;

                string displayText = isSelected ? $"✓ ПУТЬ {flowIndex}" : $"○ ПУТЬ {flowIndex}";

                Point textPosition;

                Point midPoint;
                if (points.Count == 2)
                {
                    midPoint = new Point((points[0].X + points[1].X) / 2, (points[0].Y + points[1].Y) / 2);
                }
                else
                {
                    midPoint = points[points.Count / 2];
                }

                Point direction;
                if (points.Count == 2)
                {
                    direction = new Point(points[1].X - points[0].X, points[1].Y - points[0].Y);
                }
                else
                {
                    direction = new Point(points[2].X - points[1].X, points[2].Y - points[1].Y);
                }

                if (direction.X != 0 || direction.Y != 0)
                {
                    double length = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
                    double perpX = -direction.Y / length * 20;
                    double perpY = direction.X / length * 20;

                    textPosition = new Point(
                        midPoint.X + (int)perpX,
                        midPoint.Y + (int)perpY
                    );
                }
                else
                {
                    textPosition = midPoint;
                }

                if (_gatewayFlowButtons.TryGetValue(flow.SourceRef, out var flowButtons) && flowButtons.Any())
                {
                    int maxRight = flowButtons.Max(b => b.Bounds.Right);
                    int minY = flowButtons.Min(b => b.Bounds.Y);
                    Rectangle buttonsArea = new Rectangle(maxRight, minY, 30, flowButtons.Max(b => b.Bounds.Bottom) - minY);
                    Rectangle textRect = new Rectangle(textPosition.X - 30, textPosition.Y - 10, 60, 20);
                    if (textRect.IntersectsWith(buttonsArea))
                    {
                        textPosition = new Point(buttonsArea.Right + 10, buttonsArea.Y + buttonsArea.Height / 2 - 5);
                    }
                }

                using (Font font = new Font("Segoe UI", 7, FontStyle.Bold))
                using (Brush brush = new SolidBrush(GetFlowStatusColor(isSelected)))
                using (Brush bgBrush = new SolidBrush(Color.FromArgb(220, Color.White)))
                {
                    SizeF textSize = g.MeasureString(displayText, font);

                    RectangleF textBg = new RectangleF(
                        textPosition.X - 2,
                        textPosition.Y - 2,
                        textSize.Width + 4,
                        textSize.Height + 4
                    );
                    g.FillRectangle(bgBrush, textBg);

                    using (Pen borderPen = new Pen(GetFlowStatusColor(isSelected), 1))
                    {
                        g.DrawRectangle(borderPen, textBg.X, textBg.Y, textBg.Width, textBg.Height);
                    }

                    g.DrawString(displayText, font, brush, textPosition);
                }
            }
        }


        private void DrawToken(Graphics g, Token token)
        {
            if (!nodeRectangles.ContainsKey(token.CurrentNodeId)) return;

            var rect = nodeRectangles[token.CurrentNodeId];

            int tokenSize = 16;
            int tokenX = rect.Right - tokenSize - 5;
            int tokenY = rect.Top + 5;

            using (Brush tokenBrush = new SolidBrush(Color.Red))
            {
                g.FillEllipse(tokenBrush, tokenX, tokenY, tokenSize, tokenSize);
            }

            using (Pen pen = new Pen(Color.White, 1.5f))
            {
                g.DrawEllipse(pen, tokenX, tokenY, tokenSize, tokenSize);
            }
        }

        private void DrawAllTokens(Graphics graphics)
        {
            if (_activeTokens == null || _activeTokens.Count == 0) return;

            var tokensByNode = _activeTokens
                .Where(t => !t.IsCompleted && nodeRectangles.ContainsKey(t.CurrentNodeId))
                .GroupBy(t => t.CurrentNodeId)
                .ToDictionary(group => group.Key, group => group.ToList());

            int tokenSize = 16;

            foreach (var nodeGroup in tokensByNode)
            {
                if (!nodeRectangles.TryGetValue(nodeGroup.Key, out Rectangle rect)) continue;

                int tokensCount = nodeGroup.Value.Count;

                int startX = rect.Right - tokenSize - 5;
                int startY = rect.Top + 5;

                for (int i = 0; i < tokensCount; i++)
                {
                    var token = nodeGroup.Value[i];

                    int offsetX = (i % 3) * (tokenSize + 2);
                    int offsetY = (i / 3) * (tokenSize + 2);

                    int tokenX = startX - offsetX;
                    int tokenY = startY + offsetY;

                    if (tokenX < rect.Left + 5) tokenX = rect.Left + 5;
                    if (tokenY + tokenSize > rect.Bottom - 5) tokenY = rect.Bottom - tokenSize - 5;

                    Color tokenColor = GetTokenColor(token.InstanceId);

                    using (Brush tokenBrush = new SolidBrush(tokenColor))
                    {
                        graphics.FillEllipse(tokenBrush, tokenX, tokenY, tokenSize, tokenSize);
                    }

                    using (Pen pen = new Pen(Color.White, 1.5f))
                    {
                        graphics.DrawEllipse(pen, tokenX, tokenY, tokenSize, tokenSize);
                    }

                    using (Font font = new Font("Arial", 8, FontStyle.Bold))
                    using (Brush textBrush = new SolidBrush(Color.White))
                    {
                        string instanceNum = token.InstanceId.ToString();
                        var textSize = graphics.MeasureString(instanceNum, font);
                        graphics.DrawString(instanceNum, font, textBrush,
                            tokenX + (tokenSize - textSize.Width) / 2,
                            tokenY + (tokenSize - textSize.Height) / 2);
                    }
                }
            }
        }

        private Color GetTokenColor(int tokenId)
        {
            Color[] colors = {
        Color.Red,      // Токен 1
        Color.Blue,     // Токен 2
        Color.Green,    // Токен 3
        Color.Orange,   // Токен 4
        Color.Purple,   // Токен 5
        Color.Teal,     // Токен 6
        Color.Magenta,  // Токен 7
        Color.Gold      // Токен 8
    };
            return colors[(tokenId - 1) % colors.Length];
        }

        private Color GetFlowStatusColor(bool isSelected)
        {
            return isSelected ? Color.Green : Color.Gray;
        }


        private void CanvasPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                _isDragging = true;
                _dragStartPoint = new Point(e.X, e.Y);
                canvasPanel.Cursor = Cursors.SizeAll;
            }
        }

        private void CanvasPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                int deltaX = e.X - _dragStartPoint.X;
                int deltaY = e.Y - _dragStartPoint.Y;
                canvasPanel.AutoScrollPosition = new Point(
                    -canvasPanel.AutoScrollPosition.X + deltaX,
                    -canvasPanel.AutoScrollPosition.Y + deltaY);
                canvasPanel.Invalidate();
            }
        }

        private void CanvasPanel_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                _isDragging = false;
                canvasPanel.Cursor = Cursors.Default;
            }
        }

        private void CanvasPanel_MouseClick(object sender, MouseEventArgs e)
        {
            if (_currentProcess == null) return;

            Point canvasPoint = new Point(
                (int)((e.X - canvasPanel.AutoScrollPosition.X) / _zoom),
                (int)((e.Y - canvasPanel.AutoScrollPosition.Y) / _zoom)
            );

            foreach (var gatewayButtons in _gatewayFlowButtons)
            {
                if (!_currentProcess.Nodes.ContainsKey(gatewayButtons.Key))
                    continue;

                foreach (var btn in gatewayButtons.Value)
                {
                    if (!btn.Bounds.Contains(canvasPoint))
                        continue;

                    var gateway = _currentProcess.Nodes[gatewayButtons.Key];
                    HandleGatewayFlowButtonClick(gateway, btn.FlowId);
                    return;
                }
            }

            foreach (var nodeRect in nodeRectangles)
            {
                if (nodeRect.Value.Contains(canvasPoint))
                {
                    var node = _currentProcess.Nodes[nodeRect.Key];
                    propertyGrid.SelectedObject = new NodeInfo
                    {
                        Id = node.Id,
                        Name = node.Name ?? "(не задано)",
                        Type = node.Type.ToString(),
                        TypeDescription = GetNodeTypeDescription(node.Type)
                    };
                    AddLogMessage($"🔍 Выбран узел: {node.Name ?? node.Type.ToString()}");
                    break;
                }
            }
        }

        private void HandleGatewayFlowButtonClick(BpmnNode gateway, string flowId)
        {
            var outgoingFlows = GetOutgoingFlows(gateway.Id);
            int flowIndex = outgoingFlows.FindIndex(f => f.Id == flowId) + 1;
            string flowLabel = outgoingFlows.FirstOrDefault(f => f.Id == flowId)?.Name ?? flowId;

            switch (gateway.Type)
            {
                case NodeType.ExclusiveGateway:
                    _gatewaySelectedFlows[gateway.Id] = flowId;
                    UpdateGatewayCondition(gateway.Id, flowId);
                    AddLogMessage($"🔀 XOR «{GetGatewayDisplayName(gateway)}»: путь {flowIndex} — {flowLabel}");
                    break;

                case NodeType.ParallelGateway:
                    if (_gatewaySelectedFlows.TryGetValue(gateway.Id, out var current) && current == flowId)
                    {
                        _gatewaySelectedFlows.Remove(gateway.Id);
                        AddLogMessage($"⚡ AND «{GetGatewayDisplayName(gateway)}»: все исходящие потоки (по умолчанию)");
                    }
                    else
                    {
                        _gatewaySelectedFlows[gateway.Id] = flowId;
                        AddLogMessage($"⚡ AND «{GetGatewayDisplayName(gateway)}»: только путь {flowIndex} — {flowLabel}");
                    }
                    break;

                case NodeType.InclusiveGateway:
                    if (!_gatewayInclusiveSelectedFlows.ContainsKey(gateway.Id))
                        _gatewayInclusiveSelectedFlows[gateway.Id] = new HashSet<string>();

                    var selected = _gatewayInclusiveSelectedFlows[gateway.Id];
                    if (selected.Contains(flowId))
                    {
                        selected.Remove(flowId);
                        AddLogMessage($"◇ OR «{GetGatewayDisplayName(gateway)}»: снят путь {flowIndex}");
                    }
                    else
                    {
                        selected.Add(flowId);
                        AddLogMessage($"◇ OR «{GetGatewayDisplayName(gateway)}»: добавлен путь {flowIndex} — {flowLabel}");
                    }
                    break;
            }

            SyncSimulatorGatewaySelections();
            canvasPanel.Invalidate();
        }

        private static string GetGatewayDisplayName(BpmnNode gateway)
        {
            if (!string.IsNullOrEmpty(gateway.Name))
                return gateway.Name;
            switch (gateway.Type)
            {
                case NodeType.ExclusiveGateway: return "XOR";
                case NodeType.ParallelGateway: return "AND";
                case NodeType.InclusiveGateway: return "OR";
                default: return gateway.Type.ToString();
            }
        }


        private void UpdateGatewayCondition(string gatewayId, string selectedFlowId)
        {
            var selectedFlow = _currentProcess.OutgoingFlows[gatewayId]
                .FirstOrDefault(f => f.Id == selectedFlowId);

            if (selectedFlow != null)
            {
                string condition = "${__selected__ == '" + selectedFlowId + "'}";

                foreach (var flow in _currentProcess.OutgoingFlows[gatewayId])
                {
                    if (flow.Id == selectedFlowId)
                    {
                        flow.ConditionExpression = condition;
                    }
                    else
                    {
                        flow.ConditionExpression = "${__selected__ != '" + selectedFlowId + "'}";
                    }
                }

                AddLogMessage($"  → Путь '{selectedFlow.Name ?? selectedFlow.Id}' активен");
            }
        }
        private string GetNodeTypeDescription(NodeType type)
        {
            switch (type)
            {
                case NodeType.StartEvent: return "Стартовое событие (круг, тонкая граница)";
                case NodeType.EndEvent: return "Конечное событие (круг, жирная граница)";
                case NodeType.Task: return "Задача (прямоугольник со скругленными углами)";
                case NodeType.ExclusiveGateway: return "Исключающий шлюз (X) — один исходящий поток";
                case NodeType.ParallelGateway: return "Параллельный шлюз (+) — все или выбранный поток";
                case NodeType.InclusiveGateway: return "Включающий шлюз (○) — один или несколько потоков";
                default: return type.ToString();
            }
        }

        private void StartSimulation_Click(object sender, EventArgs e)
        {
            if (_currentProcess == null)
            {
                MessageBox.Show("Сначала загрузите BPMN диаграмму", "Предупреждение",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_activeTokens != null && _activeTokens.Count > 0)
            {
                var result = MessageBox.Show(
                    "Уже есть активные экземпляры процессов. Остановить их и запустить новый?",
                    "Подтверждение",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.No)
                    return;

                StopAllProcesses();
            }

            var variables = GetVariablesFromGrid();

            _simulator = new BpmnSimulator(_currentProcess);
            SyncSimulatorGatewaySelections();

            _simulator.OnTokenMoved += Simulator_OnTokenMoved;
            _simulator.OnDecision += Simulator_OnDecision;
            _simulator.OnParallelSplit += Simulator_OnParallelSplit;
            _simulator.OnError += Simulator_OnError;
            _simulator.OnTokensUpdated += Simulator_OnTokensUpdated;

            try
            {
                Token.ResetInstanceIdCounter();

                var startToken = _simulator.StartProcess(variables);
                _activeTokens = new List<Token> { startToken };
                _simulationStep = 0;
                _isSimulating = true;

                AddLogMessage("🚀 ПРОЦЕСС ЗАПУЩЕН");
                AddLogMessage($"📋 Переменные: {string.Join(", ", variables.Select(v => $"{v.Key}={v.Value}"))}");

                if (_gatewaySelectedFlows.Any())
                {
                    AddLogMessage($"🔀 Активные выборы XOR шлюзов: {_gatewaySelectedFlows.Count}");
                }

                canvasPanel.Invalidate();

                btnStartSimulation.Enabled = false;
                btnStepSimulation.Enabled = true;
                btnResetSimulation.Enabled = true;

                if (btnAutoStep.Checked)
                {
                    int speed = int.Parse(cmbSpeed.SelectedItem.ToString());
                    simulationTimer.Interval = speed;
                    simulationTimer.Start();
                }

                UpdateStatus("Процесс запущен");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatus($"Ошибка: {ex.Message}");
            }
        }

        private void StepSimulation_Click(object sender, EventArgs e)
        {
            if (_simulator != null)
            {
                bool hasActiveTokens = _simulator.Step();
                _simulationStep++;
                canvasPanel.Invalidate();

                if (!hasActiveTokens)
                {
                    simulationTimer.Stop();
                    AddLogMessage($"✅ ВСЕ ПРОЦЕССЫ ЗАВЕРШЕНЫ за {_simulationStep} шагов");
                    UpdateStatus("Все процессы завершены");
                    btnStartSimulation.Enabled = true;
                    btnStepSimulation.Enabled = false;
                    btnResetSimulation.Enabled = true;
                    btnPauseSimulation.Enabled = false;
                }
                else
                {
                    UpdateStatus($"Шаг {_simulationStep} выполнен, активно экземпляров: {_activeTokens.Count(t => !t.IsCompleted)}");
                }
            }
        }

        private void ResetSimulation_Click(object sender, EventArgs e)
        {
            simulationTimer.Stop();
            btnAutoStep.Checked = false;
            _simulator?.Reset();
            _activeTokens.Clear();
            _simulator = null;
            _simulationStep = 0;
            _isSimulating = false;
            AddLogMessage("🔄 Симуляция сброшена");
            canvasPanel.Invalidate();
            btnStartSimulation.Enabled = true;
            btnStepSimulation.Enabled = false;
            btnResetSimulation.Enabled = false;
            UpdateStatus("Симуляция сброшена");
        }

        private void PauseSimulation()
        {
            if (simulationTimer.Enabled)
            {
                simulationTimer.Stop();
                AddLogMessage("⏸ Симуляция приостановлена");
                UpdateStatus("Симуляция приостановлена");
                btnPauseSimulation.Enabled = false;
                btnStartSimulation.Enabled = true;
            }
            else if (_isSimulating && _activeTokens != null && _activeTokens.Any(t => !t.IsCompleted))
            {
                int speed = int.Parse(cmbSpeed.SelectedItem.ToString());
                simulationTimer.Interval = speed;
                simulationTimer.Start();
                AddLogMessage("▶ Симуляция возобновлена");
                UpdateStatus("Симуляция возобновлена");
                btnPauseSimulation.Enabled = true;
                btnStartSimulation.Enabled = false;
            }
        }

        private void ZoomIn()
        {
            if (_zoom < 2.0f)
            {
                _zoom += 0.1f;
                UpdateZoomStatus();
                canvasPanel.Invalidate();
            }
        }

        private void ZoomOut()
        {
            if (_zoom > 0.3f)
            {
                _zoom -= 0.1f;
                UpdateZoomStatus();
                canvasPanel.Invalidate();
            }
        }

        private void ResetZoom()
        {
            _zoom = 1.0f;
            UpdateZoomStatus();
            canvasPanel.Invalidate();
        }

        private void FitToScreen()
        {
            if (_currentProcess == null || nodeRectangles.Count == 0) return;

            int minX = nodeRectangles.Values.Min(r => r.X);
            int minY = nodeRectangles.Values.Min(r => r.Y);
            int maxX = nodeRectangles.Values.Max(r => r.Right);
            int maxY = nodeRectangles.Values.Max(r => r.Bottom);

            if (_pools != null && _pools.Any())
            {
                minX = Math.Min(minX, _pools.Min(p => p.Bounds.X));
                minY = Math.Min(minY, _pools.Min(p => p.Bounds.Y));
                maxX = Math.Max(maxX, _pools.Max(p => p.Bounds.Right));
                maxY = Math.Max(maxY, _pools.Max(p => p.Bounds.Bottom));
            }

            int diagramWidth = maxX - minX + 150;
            int diagramHeight = maxY - minY + 150;

            int visibleWidth = mainSplitContainer.Panel1.Width - 30;
            int visibleHeight = mainSplitContainer.Panel1.Height - 30;

            float zoomX = (float)visibleWidth / diagramWidth;
            float zoomY = (float)visibleHeight / diagramHeight;
            _zoom = Math.Min(zoomX, zoomY);
            _zoom = Math.Max(0.3f, Math.Min(2.0f, _zoom));

            UpdateZoomStatus();
            canvasPanel.Invalidate();
        }

        private void UpdateZoomStatus()
        {
            if (statusStrip.InvokeRequired)
            {
                statusStrip.Invoke(new Action(UpdateZoomStatus));
                return;
            }

            var zoomLabel = (ToolStripStatusLabel)statusStrip.Items[4];
            zoomLabel.Text = $"Масштаб: {_zoom * 100:F0}%";
        }

        private void SimulationTimer_Tick(object sender, EventArgs e)
        {
            if (_simulator != null)
            {
                bool hasActiveTokens = _simulator.Step();
                _simulationStep++;
                canvasPanel.Invalidate();

                if (!hasActiveTokens)
                {
                    simulationTimer.Stop();
                    AddLogMessage($"✅ ВСЕ ПРОЦЕССЫ ЗАВЕРШЕНЫ за {_simulationStep} шагов");
                    UpdateStatus("Все процессы завершены");
                    btnStartSimulation.Enabled = true;
                    btnStepSimulation.Enabled = false;
                    btnResetSimulation.Enabled = true;
                    btnPauseSimulation.Enabled = false;
                    btnAutoStep.Checked = false;
                }
            }
        }

        private void AddLogMessage(string message)
        {
            if (logListBox.InvokeRequired)
            {
                logListBox.Invoke(new Action<string>(AddLogMessage), message);
                return;
            }

            logListBox.Items.Add($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
            logListBox.TopIndex = logListBox.Items.Count - 1;
        }

        private void UpdateStatus(string message)
        {
            if (statusStrip.InvokeRequired)
            {
                statusStrip.Invoke(new Action<string>(UpdateStatus), message);
                return;
            }

            var statusLabel = (ToolStripStatusLabel)statusStrip.Items[2];
            statusLabel.Text = message;
        }

        private void UpdateButtonsState()
        {
            bool hasActiveTokens = _activeTokens != null && _activeTokens.Any(t => !t.IsCompleted);
            bool hasAnyTokens = _activeTokens != null && _activeTokens.Count > 0;

            btnStartSimulation.Enabled = _currentProcess != null;
            btnStepSimulation.Enabled = hasActiveTokens;
            btnResetSimulation.Enabled = hasAnyTokens;
            btnPauseSimulation.Enabled = hasActiveTokens && simulationTimer.Enabled;
            btnAutoStep.Enabled = hasActiveTokens;
        }

        private void UpdateStatusWithTokens()
        {
            if (_activeTokens == null || _activeTokens.Count == 0)
            {
                UpdateStatus("Нет активных процессов");
                return;
            }

            var activeInstances = _activeTokens.Where(t => !t.IsCompleted).Select(t => t.InstanceId).Distinct().ToList();
            var completedInstances = _activeTokens.Where(t => t.IsCompleted).Select(t => t.InstanceId).Distinct().ToList();

            string status = $"Активно: {activeInstances.Count} экз. [{string.Join(",", activeInstances)}]";
            if (completedInstances.Any())
                status += $" | Завершено: {completedInstances.Count} экз. [{string.Join(",", completedInstances)}]";

            UpdateStatus(status);
        }

        private void Simulator_OnTokenMoved(string fromNodeId, string toNodeId)
        {
            string fromName = fromNodeId != null && _currentProcess.Nodes.ContainsKey(fromNodeId)
                ? _currentProcess.Nodes[fromNodeId].Name ?? fromNodeId : "Старт";
            string toName = toNodeId != null && _currentProcess.Nodes.ContainsKey(toNodeId)
                ? _currentProcess.Nodes[toNodeId].Name ?? toNodeId : "Финиш";

            var movedToken = _activeTokens.FirstOrDefault(t => t.CurrentNodeId == toNodeId);
            if (movedToken != null)
            {
                AddLogMessage($"📍 Экземпляр #{movedToken.InstanceId}: {fromName} → {toName}");
            }
        }

        private void Simulator_OnDecision(string gatewayId, string selectedFlowId)
        {
            if (_currentProcess.Nodes.ContainsKey(gatewayId))
            {
                var gateway = _currentProcess.Nodes[gatewayId];

                AddLogMessage($"🔀 {GetGatewayDisplayName(gateway)}: токен по потоку {selectedFlowId}");
            }
        }

        private void Simulator_OnParallelSplit(string gatewayId, List<string> targetNodeIds)
        {
            if (_currentProcess.Nodes.ContainsKey(gatewayId))
            {
                var gateway = _currentProcess.Nodes[gatewayId];
                AddLogMessage($"⚡ Параллельное разделение в '{gateway.Name ?? gateway.Type.ToString()}' на {targetNodeIds.Count} потока");
            }
        }

        private void Simulator_OnError(string nodeId, string error)
        {
            AddLogMessage($"❌ ОШИБКА: {error}");
            UpdateStatus($"Ошибка: {error}");
        }

        private void Simulator_OnTokensUpdated(List<Token> tokens)
        {
            _activeTokens = tokens;
            canvasPanel.Invalidate();

            var instances = tokens.Select(t => t.InstanceId).Distinct().ToList();
            var activeInstances = tokens.Where(t => !t.IsCompleted).Select(t => t.InstanceId).Distinct().ToList();
            var completedInstances = tokens.Where(t => t.IsCompleted).Select(t => t.InstanceId).Distinct().ToList();

            if (instances.Count > 1)
            {
                AddLogMessage($"📊 Всего экземпляров: {instances.Count} ({string.Join(", ", instances.Select(i => $"#{i}"))})");
                if (activeInstances.Any())
                    AddLogMessage($"▶ Активные: {string.Join(", ", activeInstances.Select(i => $"#{i}"))}");
                if (completedInstances.Any())
                    AddLogMessage($"✅ Завершены: {string.Join(", ", completedInstances.Select(i => $"#{i}"))}");
            }
            else if (instances.Count == 1)
            {
                if (!tokens[0].IsCompleted)
                    AddLogMessage($"▶ Экземпляр #{tokens[0].InstanceId} активен");
                else
                    AddLogMessage($"✅ Экземпляр #{tokens[0].InstanceId} завершен");
            }

            UpdateStatusWithTokens();
            UpdateButtonsState();

            if (activeInstances.Count == 0 && btnAutoStep.Checked)
            {
                if (simulationTimer.Enabled)
                {
                    simulationTimer.Stop();
                    btnAutoStep.Checked = false;
                    AddLogMessage("🤖 Автошаг отключен (все процессы завершены)");
                }
            }
        }

        private void StartNewProcessInstance()
        {
            if (_currentProcess == null)
            {
                MessageBox.Show("Сначала загрузите BPMN диаграмму", "Предупреждение",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var variables = GetVariablesFromGrid();

            if (_simulator == null)
            {
                _simulator = new BpmnSimulator(_currentProcess);
                SyncSimulatorGatewaySelections();

                _simulator.OnTokenMoved += Simulator_OnTokenMoved;
                _simulator.OnDecision += Simulator_OnDecision;
                _simulator.OnParallelSplit += Simulator_OnParallelSplit;
                _simulator.OnError += Simulator_OnError;
                _simulator.OnTokensUpdated += Simulator_OnTokensUpdated;
            }
            else
            {
                SyncSimulatorGatewaySelections();
            }

            try
            {
                var newToken = _simulator.StartProcess(variables);

                AddLogMessage($"🚀 ЗАПУЩЕН НОВЫЙ ЭКЗЕМПЛЯР ПРОЦЕССА #{newToken.InstanceId}");
                AddLogMessage($"📋 Переменные: {string.Join(", ", variables.Select(v => $"{v.Key}={v.Value}"))}");

                canvasPanel.Invalidate();

                if (btnAutoStep.Checked)
                {
                    if (simulationTimer.Enabled)
                        simulationTimer.Stop();

                    int speed = int.Parse(cmbSpeed.SelectedItem.ToString());
                    simulationTimer.Interval = speed;
                    simulationTimer.Start();
                    AddLogMessage("🤖 Автошаг запущен автоматически");
                }

                UpdateStatus($"Запущен экземпляр #{newToken.InstanceId}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatus($"Ошибка: {ex.Message}");
            }
        }

        private Dictionary<string, object> GetVariablesFromGrid()
        {
            var variables = new Dictionary<string, object>();
            foreach (DataGridViewRow row in variablesGridView.Rows)
            {
                if (row.Cells[0].Value != null && !string.IsNullOrEmpty(row.Cells[0].Value.ToString()))
                {
                    string varName = row.Cells[0].Value.ToString();
                    string varValue = row.Cells[1].Value?.ToString() ?? "";

                    if (int.TryParse(varValue, out int intVal))
                        variables[varName] = intVal;
                    else if (decimal.TryParse(varValue, out decimal decVal))
                        variables[varName] = decVal;
                    else if (bool.TryParse(varValue, out bool boolVal))
                        variables[varName] = boolVal;
                    else
                        variables[varName] = varValue;
                }
            }
            return variables;
        }

        private void InitializeAnimationTimer()
        {
            _animationTimer = new Timer();
            _animationTimer.Interval = 200;
            _animationTimer.Tick += (s, e) =>
            {
                if (_activeTokens != null && _activeTokens.Any(t => !t.IsCompleted) && _isSimulating)
                {
                    canvasPanel.Invalidate();
                }
            };
            _animationTimer.Start();
        }

        private void InitializeAnimations()
        {
            InitializeAnimationTimer();
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_animationTimer != null)
            {
                _animationTimer.Stop();
                _animationTimer.Dispose();
                _animationTimer = null;
            }

            base.OnFormClosing(e);
        }

        private void StopAllProcesses()
        {
            if (_simulator != null)
            {
                simulationTimer.Stop();
                _simulator.Reset();
                _activeTokens.Clear();
                _simulationStep = 0;
                _isSimulating = false;
                AddLogMessage("⏹ ВСЕ ПРОЦЕССЫ ОСТАНОВЛЕНЫ");
                canvasPanel.Invalidate();
                UpdateStatus("Все процессы остановлены");
                UpdateButtonsState();
            }
        }

    }

    public class NodeInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string TypeDescription { get; set; }
    }

    public class GatewayFlowButton
    {
        public string FlowId { get; set; }
        public Rectangle Bounds { get; set; }
    }
}