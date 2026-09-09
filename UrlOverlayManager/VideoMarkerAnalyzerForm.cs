using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UrlOverlayManager
{
    public class VideoMarkerAnalyzerForm : Form
    {
        private const int SideButtonHeight = 28;
        private const int SideButtonRowHeight = 34;
        private const int SideSectionTitleHeight = 22;
        private const int SideSectionPaddingTop = 5;
        private const int SideSectionPaddingBottom = 7;
        private const int SideSectionMarginBottom = 8;
        private const int SideSectionGuard = 8;
        private const int SideInnerGap = 6;

        private readonly WebView2 videoView = new WebView2();
        private readonly DataGridView markerGrid = new DataGridView();
        private readonly BindingSource markerBindingSource = new BindingSource();
        private readonly Label lblFile = new Label();
        private readonly Label lblStatus = new Label();
        private readonly Button btnLoadVideo = new Button();
        private readonly Button btnAnalyze = new Button();
        private readonly Button btnPrev = new Button();
        private readonly Button btnNext = new Button();
        private readonly Button btnAddManual = new Button();
        private readonly Button btnAccept = new Button();
        private readonly Button btnHold = new Button();
        private readonly Button btnReject = new Button();
        private readonly Button btnExport = new Button();
        private readonly NumericUpDown numPreroll = new NumericUpDown();
        private readonly NumericUpDown numMaxMarkers = new NumericUpDown();
        private readonly NumericUpDown numMinGap = new NumericUpDown();
        private readonly ComboBox cboSensitivity = new ComboBox();

        private readonly List<VideoMarkerCandidate> markers = new List<VideoMarkerCandidate>();
        private readonly Dictionary<string, string> previewCache = new Dictionary<string, string>();
        private string currentVideoPath = "";
        private bool isWebViewReady;
        private bool isAnalyzing;
        private int previewRequestId;
        private double currentPlayerOffsetSeconds;

        public VideoMarkerAnalyzerForm(bool hostedMode = false)
        {
            InitializeForm();
            BuildLayout();
            UiTheme.ApplyFonts(this);

            if (!hostedMode)
            {
                UiChrome.Apply(this, "편집점 후보 탐색기", true);
            }

            InitGrid();
            _ = InitializeBrowserAsync();
        }

        public void ApplyCurrentUiSettings()
        {
            UiTheme.ApplyFonts(this);
            UiTheme.StyleGrid(markerGrid);
            UiTheme.StyleNumeric(numPreroll);
            UiTheme.StyleNumeric(numMaxMarkers);
            UiTheme.StyleNumeric(numMinGap);
            UiTheme.StyleComboBox(cboSensitivity);
        }

        private void InitializeForm()
        {
            Text = "편집점 후보 탐색기";
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1180, 760);
            MinimumSize = new Size(1020, 650);
            BackColor = UiTheme.AppBackColor;
            ForeColor = UiTheme.TextColor;
            Font = UiTheme.RegularFont();
            KeyPreview = true;
            KeyDown += VideoMarkerAnalyzerForm_KeyDown;
        }

        private void BuildLayout()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(12);
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));

            Panel header = new Panel();
            header.Dock = DockStyle.Fill;

            Label title = new Label();
            title.Text = "편집점 후보 탐색기";
            title.Font = UiTheme.BoldFont(14F);
            title.ForeColor = UiTheme.TextColor;
            title.Location = new Point(0, 0);
            title.Size = new Size(300, 24);

            lblFile.Text = "불러온 영상이 없습니다.";
            lblFile.ForeColor = UiTheme.MutedTextColor;
            lblFile.Location = new Point(2, 29);
            lblFile.Size = new Size(820, 20);

            header.Controls.Add(title);
            header.Controls.Add(lblFile);

            numPreroll.Minimum = 0;
            numPreroll.Maximum = 60;
            numPreroll.Value = 15;
            UiTheme.StyleNumeric(numPreroll);

            cboSensitivity.DropDownStyle = ComboBoxStyle.DropDownList;
            cboSensitivity.Items.AddRange(new object[] { "보수", "보통", "민감" });
            cboSensitivity.SelectedIndex = 0;
            UiTheme.StyleComboBox(cboSensitivity);

            ConfigureNumber(numMaxMarkers, 20, 300, 60, 70);
            ConfigureNumber(numMinGap, 10, 180, 45, 70);

            btnLoadVideo.Click += LoadVideoButton_Click;
            btnAnalyze.Click += AnalyzeButton_Click;
            btnPrev.Click += PrevButton_Click;
            btnNext.Click += NextButton_Click;
            btnAddManual.Click += AddManualButton_Click;
            btnAccept.Click += (s, e) => SetSelectedStatus("채택");
            btnHold.Click += (s, e) => SetSelectedStatus("보류");
            btnReject.Click += (s, e) => SetSelectedStatus("버림");
            btnExport.Click += ExportButton_Click;

            ConfigurePanelButton(btnLoadVideo, "영상 불러오기", UiTheme.AccentColor, Color.White);
            ConfigurePanelButton(btnAnalyze, "후보 분석", UiTheme.AccentColor, Color.White);
            ConfigurePanelButton(btnPrev, "이전 후보", UiTheme.PanelBackColor, UiTheme.TextColor);
            ConfigurePanelButton(btnNext, "다음 후보", UiTheme.PanelBackColor, UiTheme.TextColor);
            ConfigurePanelButton(btnAddManual, "현재 시점 추가", UiTheme.PanelBackColor, UiTheme.TextColor);
            ConfigurePanelButton(btnAccept, "채택", UiTheme.AccentColor, Color.White);
            ConfigurePanelButton(btnHold, "보류", UiTheme.PanelBackColor, UiTheme.TextColor);
            ConfigurePanelButton(btnReject, "버림", UiTheme.DangerColor, Color.White);
            ConfigurePanelButton(btnExport, "CSV 내보내기", UiTheme.PanelBackColor, UiTheme.TextColor);

            Panel videoPanel = new Panel();
            videoPanel.Dock = DockStyle.Fill;
            videoPanel.Margin = new Padding(0, 0, 0, 8);
            videoPanel.Padding = new Padding(1);
            videoPanel.BackColor = UiTheme.BorderColor;
            videoView.Dock = DockStyle.Fill;
            videoPanel.Controls.Add(videoView);

            TableLayoutPanel mediaArea = new TableLayoutPanel();
            mediaArea.Dock = DockStyle.Fill;
            mediaArea.ColumnCount = 1;
            mediaArea.RowCount = 2;
            mediaArea.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
            mediaArea.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
            mediaArea.Margin = new Padding(0, 0, 10, 0);

            markerGrid.Dock = DockStyle.Fill;
            markerGrid.Margin = new Padding(0);
            mediaArea.Controls.Add(videoPanel, 0, 0);
            mediaArea.Controls.Add(markerGrid, 0, 1);

            TableLayoutPanel body = new TableLayoutPanel();
            body.Dock = DockStyle.Fill;
            body.ColumnCount = 2;
            body.RowCount = 1;
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 292));
            body.Controls.Add(mediaArea, 0, 0);
            body.Controls.Add(CreateMarkerCommandPanel(), 1, 0);

            lblStatus.Dock = DockStyle.Fill;
            lblStatus.ForeColor = UiTheme.MutedTextColor;
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblStatus.Text = "";

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(body, 0, 1);
            root.Controls.Add(lblStatus, 0, 2);

            Controls.Add(root);
        }

        private Control CreateMarkerCommandPanel()
        {
            TableLayoutPanel panel = CreateSidePanel();

            TableLayoutPanel analysis = CreateSideSection("분석", 5);
            analysis.Controls.Add(btnLoadVideo, 0, 1);
            analysis.Controls.Add(btnAnalyze, 0, 2);
            Control analysisOptions = CreateAnalysisOptionsGrid();
            analysis.Controls.Add(analysisOptions, 0, 3);
            analysis.SetRowSpan(analysisOptions, 2);

            TableLayoutPanel navigation = CreateSideSection("후보 이동", 3);
            TableLayoutPanel navigationButtons = CreateButtonGrid(2);
            navigationButtons.Controls.Add(btnPrev, 0, 0);
            navigationButtons.Controls.Add(btnNext, 2, 0);
            navigation.Controls.Add(navigationButtons, 0, 1);
            navigation.Controls.Add(btnAddManual, 0, 2);

            TableLayoutPanel decision = CreateSideSection("판정", 2);
            TableLayoutPanel decisionButtons = CreateButtonGrid(3);
            decisionButtons.Controls.Add(btnAccept, 0, 0);
            decisionButtons.Controls.Add(btnHold, 2, 0);
            decisionButtons.Controls.Add(btnReject, 4, 0);
            decision.Controls.Add(decisionButtons, 0, 1);

            TableLayoutPanel export = CreateSideSection("내보내기", 2);
            export.Controls.Add(btnExport, 0, 1);

            panel.Controls.Add(analysis, 0, 0);
            panel.Controls.Add(navigation, 0, 1);
            panel.Controls.Add(decision, 0, 2);
            panel.Controls.Add(export, 0, 3);
            return panel;
        }

        private Control CreateAnalysisOptionsGrid()
        {
            TableLayoutPanel grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.ColumnCount = 4;
            grid.RowCount = 2;
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            grid.Margin = new Padding(0, 4, 0, 0);

            AddOptionControl(grid, "앞당김", numPreroll, 0, 0);
            AddOptionControl(grid, "민감도", cboSensitivity, 2, 0);
            AddOptionControl(grid, "최대", numMaxMarkers, 0, 1);
            AddOptionControl(grid, "간격", numMinGap, 2, 1);
            return grid;
        }

        private void InitGrid()
        {
            markerGrid.AutoGenerateColumns = false;
            markerGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            markerGrid.MultiSelect = false;
            markerGrid.AllowUserToAddRows = false;
            markerGrid.AllowUserToDeleteRows = false;
            markerGrid.ReadOnly = false;
            UiTheme.StyleGrid(markerGrid);
            markerGrid.CellDoubleClick += MarkerGrid_CellDoubleClick;
            markerGrid.SelectionChanged += MarkerGrid_SelectionChanged;

            markerGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "시간",
                DataPropertyName = nameof(VideoMarkerCandidate.DisplayTime),
                ReadOnly = true,
                Width = 90
            });

            markerGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "유형",
                DataPropertyName = nameof(VideoMarkerCandidate.Kind),
                ReadOnly = true,
                Width = 110
            });

            markerGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "점수",
                DataPropertyName = nameof(VideoMarkerCandidate.Score),
                ReadOnly = true,
                Width = 60
            });

            markerGrid.Columns.Add(new DataGridViewComboBoxColumn
            {
                HeaderText = "상태",
                DataPropertyName = nameof(VideoMarkerCandidate.Status),
                DataSource = new[] { "후보", "채택", "보류", "버림" },
                Width = 76
            });

            markerGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "이유",
                DataPropertyName = nameof(VideoMarkerCandidate.Reason),
                ReadOnly = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });

            markerGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "메모",
                DataPropertyName = nameof(VideoMarkerCandidate.Note),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });

            markerBindingSource.DataSource = markers;
            markerGrid.DataSource = markerBindingSource;
        }

        private async Task InitializeBrowserAsync()
        {
            try
            {
                await videoView.EnsureCoreWebView2Async();
                videoView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                videoView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                isWebViewReady = true;
                LoadEmptyPlayer();
            }
            catch (Exception ex)
            {
                lblStatus.Text = "영상 플레이어를 초기화하지 못했습니다. " + ex.Message;
            }
        }

        private void LoadEmptyPlayer()
        {
            if (!isWebViewReady)
                return;

            videoView.NavigateToString(CreatePlayerHtml(""));
        }

        private void LoadVideoButton_Click(object? sender, EventArgs e)
        {
            using OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "영상 파일 불러오기";
            dialog.Filter = "영상 파일 (*.mp4;*.mkv;*.mov;*.webm;*.avi)|*.mp4;*.mkv;*.mov;*.webm;*.avi|모든 파일 (*.*)|*.*";

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            currentVideoPath = dialog.FileName;
            currentPlayerOffsetSeconds = 0;
            previewCache.Clear();
            lblFile.Text = Path.GetFileName(currentVideoPath);
            markers.Clear();
            RefreshMarkers();
            NavigateVideo();
            lblStatus.Text = "영상을 불러왔습니다. 후보 분석을 누르면 로컬 오디오 신호를 기준으로 시점을 찾습니다.";
        }

        private void NavigateVideo()
        {
            if (!isWebViewReady || string.IsNullOrWhiteSpace(currentVideoPath))
                return;

            string fileUrl = new Uri(currentVideoPath).AbsoluteUri;
            currentPlayerOffsetSeconds = 0;
            videoView.NavigateToString(CreatePlayerHtml(fileUrl));
        }

        private async void AnalyzeButton_Click(object? sender, EventArgs e)
        {
            if (isAnalyzing)
                return;

            if (string.IsNullOrWhiteSpace(currentVideoPath))
            {
                MessageBox.Show(this, "먼저 영상 파일을 불러와 주세요.", "후보 분석");
                return;
            }

            isAnalyzing = true;
            SetAnalysisUi(false);
            lblStatus.Text = "오디오를 분석하고 있습니다. 긴 영상은 시간이 걸릴 수 있습니다.";

            try
            {
                AnalysisOptions options = GetAnalysisOptions();
                List<VideoMarkerCandidate> result = await Task.Run(() => AnalyzeVideoWithFfmpeg(currentVideoPath, options));
                markers.Clear();
                markers.AddRange(result);
                RefreshMarkers();
                lblStatus.Text = markers.Count + "개의 후보 시점을 찾았습니다. 표에서 후보를 선택하면 해당 시점으로 이동합니다.";
            }
            catch (FileNotFoundException)
            {
                lblStatus.Text = "FFmpeg를 찾지 못했습니다. 영상 재생, 수동 마커, CSV 내보내기는 계속 사용할 수 있습니다.";
                MessageBox.Show(this, "자동 분석에는 FFmpeg가 필요합니다. 프로그램 폴더의 Tools\\ffmpeg.exe 또는 PATH에서 찾을 수 있어야 합니다.", "후보 분석");
            }
            catch (Exception ex)
            {
                lblStatus.Text = "분석 중 오류가 발생했습니다. " + ex.Message;
                MessageBox.Show(this, ex.Message, "후보 분석 오류");
            }
            finally
            {
                isAnalyzing = false;
                SetAnalysisUi(true);
            }
        }

        private static List<VideoMarkerCandidate> AnalyzeVideoWithFfmpeg(string videoPath, AnalysisOptions options)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = ResolveFfmpegPath(),
                Arguments = "-hide_banner -loglevel error -i " + QuoteArgument(videoPath) + " -vn -ac 1 -ar 8000 -f s16le -",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using Process process = new Process();
            process.StartInfo = startInfo;

            try
            {
                process.Start();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                throw new FileNotFoundException("FFmpeg를 실행할 수 없습니다.", ex);
            }

            List<AudioWindow> windows = ReadAudioWindows(process.StandardOutput.BaseStream, 8000);
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "FFmpeg 분석에 실패했습니다." : error.Trim());
            }

            return BuildCandidates(windows, options);
        }

        private static List<AudioWindow> ReadAudioWindows(Stream stream, int sampleRate)
        {
            int bytesPerSecond = sampleRate * 2;
            byte[] buffer = new byte[bytesPerSecond];
            List<AudioWindow> windows = new List<AudioWindow>();
            int second = 0;

            while (true)
            {
                int read = FillBuffer(stream, buffer, bytesPerSecond);

                if (read <= 0)
                    break;

                int samples = read / 2;
                double sumSquares = 0;
                double peak = 0;

                for (int i = 0; i < samples; i++)
                {
                    short value = BitConverter.ToInt16(buffer, i * 2);
                    double normalized = Math.Abs(value / 32768.0);
                    sumSquares += normalized * normalized;
                    peak = Math.Max(peak, normalized);
                }

                double rms = samples > 0 ? Math.Sqrt(sumSquares / samples) : 0;
                windows.Add(new AudioWindow(second, rms, peak));
                second++;

                if (read < bytesPerSecond)
                    break;
            }

            return windows;
        }

        private static int FillBuffer(Stream stream, byte[] buffer, int targetBytes)
        {
            int offset = 0;

            while (offset < targetBytes)
            {
                int read = stream.Read(buffer, offset, targetBytes - offset);

                if (read <= 0)
                    break;

                offset += read;
            }

            return offset;
        }

        private static List<VideoMarkerCandidate> BuildCandidates(List<AudioWindow> windows, AnalysisOptions options)
        {
            if (windows.Count == 0)
                return new List<VideoMarkerCandidate>();

            double avgRms = windows.Average(window => window.Rms);
            double stdRms = Math.Sqrt(windows.Average(window => Math.Pow(window.Rms - avgRms, 2)));
            double avgPeak = windows.Average(window => window.Peak);
            double spikeThreshold = Math.Max(avgRms + stdRms * options.SpikeStdMultiplier, avgRms * options.SpikeAverageMultiplier);
            double activeThreshold = Math.Max(avgRms + stdRms * options.ActiveStdMultiplier, avgRms * options.ActiveAverageMultiplier);
            double silenceThreshold = Math.Max(0.006, avgRms * 0.32);
            List<VideoMarkerCandidate> candidates = new List<VideoMarkerCandidate>();

            for (int i = 1; i < windows.Count; i++)
            {
                AudioWindow current = windows[i];
                AudioWindow previous = windows[i - 1];

                if (current.Rms >= spikeThreshold && current.Peak >= Math.Max(avgPeak * 1.6, 0.16))
                {
                    AddCandidate(candidates, current.Second, "소리 급상승", Score(current.Rms, spikeThreshold), "평균보다 큰 소리 변화가 감지되었습니다.", options.MinGapSeconds);
                }

                if (i >= 8)
                {
                    bool wasSilent = windows.Skip(i - 8).Take(7).All(window => window.Rms <= silenceThreshold);

                    if (wasSilent && current.Rms >= activeThreshold)
                    {
                        AddCandidate(candidates, current.Second, "침묵 종료", Score(current.Rms, activeThreshold), "조용한 구간 뒤에 소리가 다시 커졌습니다.", options.MinGapSeconds);
                    }
                }

                double growth = previous.Rms <= 0.0001 ? current.Rms : current.Rms / previous.Rms;

                if (growth >= options.GrowthMultiplier && current.Rms >= activeThreshold)
                {
                    AddCandidate(candidates, current.Second, "리액션 의심", Math.Min(100, (int)(growth * 14)), "직전보다 소리가 급격히 커졌습니다.", options.MinGapSeconds);
                }
            }

            return candidates
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Seconds)
                .Take(options.MaxCandidates)
                .OrderBy(candidate => candidate.Seconds)
                .ToList();
        }

        private static void AddCandidate(List<VideoMarkerCandidate> candidates, int second, string kind, int score, string reason, int minGapSeconds)
        {
            VideoMarkerCandidate? near = candidates
                .Where(candidate => Math.Abs(candidate.Seconds - second) <= minGapSeconds)
                .OrderByDescending(candidate => candidate.Score)
                .FirstOrDefault();

            if (near == null)
            {
                candidates.Add(new VideoMarkerCandidate
                {
                    Seconds = second,
                    Kind = kind,
                    Score = score,
                    Status = "후보",
                    Reason = reason
                });
                return;
            }

            if (score > near.Score)
            {
                near.Seconds = second;
                near.Kind = kind;
                near.Score = score;
                near.Reason = reason;
            }
        }

        private static int Score(double value, double threshold)
        {
            if (threshold <= 0)
                return 50;

            return Math.Max(1, Math.Min(100, (int)Math.Round(value / threshold * 55)));
        }

        private AnalysisOptions GetAnalysisOptions()
        {
            string sensitivity = cboSensitivity.SelectedItem?.ToString() ?? "보수";

            AnalysisOptions options = sensitivity switch
            {
                "민감" => AnalysisOptions.CreateSensitive(),
                "보통" => AnalysisOptions.CreateBalanced(),
                _ => AnalysisOptions.CreateConservative()
            };

            options.MaxCandidates = Decimal.ToInt32(numMaxMarkers.Value);
            options.MinGapSeconds = Decimal.ToInt32(numMinGap.Value);
            return options;
        }

        private void PrevButton_Click(object? sender, EventArgs e)
        {
            if (markerGrid.Rows.Count == 0)
                return;

            int rowIndex = markerGrid.CurrentRow?.Index ?? 0;
            SelectGridRow(Math.Max(0, rowIndex - 1));
        }

        private void NextButton_Click(object? sender, EventArgs e)
        {
            if (markerGrid.Rows.Count == 0)
                return;

            int rowIndex = markerGrid.CurrentRow?.Index ?? -1;
            SelectGridRow(Math.Min(markerGrid.Rows.Count - 1, rowIndex + 1));
        }

        private async void AddManualButton_Click(object? sender, EventArgs e)
        {
            double seconds = await GetCurrentVideoSecondsAsync();

            markers.Add(new VideoMarkerCandidate
            {
                Seconds = Math.Max(0, (int)Math.Round(seconds)),
                Kind = "수동 마커",
                Score = 100,
                Status = "채택",
                Reason = "사용자가 직접 추가한 시점입니다."
            });

            markers.Sort((left, right) => left.Seconds.CompareTo(right.Seconds));
            RefreshMarkers();
            SelectMarkerBySecond((int)Math.Round(seconds));
        }

        private void SetSelectedStatus(string status)
        {
            VideoMarkerCandidate? marker = GetSelectedMarker();

            if (marker == null)
                return;

            marker.Status = status;
            markerBindingSource.ResetBindings(false);
        }

        private void ExportButton_Click(object? sender, EventArgs e)
        {
            if (markers.Count == 0)
            {
                MessageBox.Show(this, "내보낼 후보 시점이 없습니다.", "CSV 내보내기");
                return;
            }

            using SaveFileDialog dialog = new SaveFileDialog();
            dialog.Title = "편집점 후보 CSV 내보내기";
            dialog.Filter = "CSV 파일 (*.csv)|*.csv";
            dialog.FileName = "video-markers.csv";

            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            File.WriteAllText(dialog.FileName, BuildCsv(), new UTF8Encoding(true));
            lblStatus.Text = "CSV 파일로 내보냈습니다. " + dialog.FileName;
        }

        private string BuildCsv()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("time,seconds,kind,score,status,reason,note");

            foreach (VideoMarkerCandidate marker in markers.OrderBy(marker => marker.Seconds))
            {
                builder.Append(Csv(marker.DisplayTime));
                builder.Append(',');
                builder.Append(marker.Seconds.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(Csv(marker.Kind));
                builder.Append(',');
                builder.Append(marker.Score.ToString(CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.Append(Csv(marker.Status));
                builder.Append(',');
                builder.Append(Csv(marker.Reason));
                builder.Append(',');
                builder.AppendLine(Csv(marker.Note));
            }

            return builder.ToString();
        }

        private static string Csv(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
        }

        private void MarkerGrid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            SelectGridRow(e.RowIndex);
        }

        private void MarkerGrid_SelectionChanged(object? sender, EventArgs e)
        {
            VideoMarkerCandidate? marker = GetSelectedMarker();

            if (marker == null)
                return;

            _ = SeekToMarkerAsync(marker);
        }

        private void SelectGridRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= markerGrid.Rows.Count)
                return;

            markerGrid.ClearSelection();
            markerGrid.Rows[rowIndex].Selected = true;
            markerGrid.CurrentCell = markerGrid.Rows[rowIndex].Cells[0];
            markerGrid.FirstDisplayedScrollingRowIndex = Math.Max(0, rowIndex - 2);
        }

        private void SelectMarkerBySecond(int seconds)
        {
            for (int i = 0; i < markerGrid.Rows.Count; i++)
            {
                if (markerGrid.Rows[i].DataBoundItem is VideoMarkerCandidate marker && marker.Seconds == seconds)
                {
                    SelectGridRow(i);
                    return;
                }
            }
        }

        private VideoMarkerCandidate? GetSelectedMarker()
        {
            if (markerGrid.CurrentRow?.DataBoundItem is VideoMarkerCandidate marker)
                return marker;

            return null;
        }

        private async Task SeekToMarkerAsync(VideoMarkerCandidate marker)
        {
            if (!isWebViewReady || string.IsNullOrWhiteSpace(currentVideoPath))
                return;

            int preroll = Decimal.ToInt32(numPreroll.Value);
            int requestId = ++previewRequestId;
            double start = Math.Max(0, marker.Seconds - preroll);

            try
            {
                lblStatus.Text = marker.DisplayTime + " 후보 주변 프리뷰를 준비하고 있습니다.";
                string previewPath = await Task.Run(() => GetOrCreatePreviewClip(currentVideoPath, marker.Seconds, preroll));

                if (requestId != previewRequestId)
                    return;

                currentPlayerOffsetSeconds = start;
                videoView.NavigateToString(CreatePlayerHtml(new Uri(previewPath).AbsoluteUri));
                lblStatus.Text = marker.DisplayTime + " 후보 프리뷰를 열었습니다. " + marker.Reason;
            }
            catch
            {
                currentPlayerOffsetSeconds = 0;
                string script = "window.seekVideo && window.seekVideo(" + start.ToString(CultureInfo.InvariantCulture) + ");";
                await videoView.ExecuteScriptAsync(script);
                lblStatus.Text = marker.DisplayTime + " 후보로 이동했습니다. 프리뷰 생성은 실패해서 원본 재생을 시도했습니다.";
            }
        }

        private async Task<double> GetCurrentVideoSecondsAsync()
        {
            if (!isWebViewReady)
                return 0;

            string json = await videoView.ExecuteScriptAsync("window.getVideoTime ? window.getVideoTime() : 0");

            if (double.TryParse(json, NumberStyles.Any, CultureInfo.InvariantCulture, out double seconds))
                return currentPlayerOffsetSeconds + seconds;

            return 0;
        }

        private string GetOrCreatePreviewClip(string videoPath, int markerSeconds, int preroll)
        {
            int start = Math.Max(0, markerSeconds - preroll);
            int duration = Math.Max(45, preroll + 60);
            string cacheKey = start + "_" + duration;

            if (previewCache.TryGetValue(cacheKey, out string? cachedPath) && File.Exists(cachedPath))
                return cachedPath;

            string cacheDirectory = Path.Combine(Path.GetTempPath(), "UrlOverlayManager", "VideoPreviews");
            Directory.CreateDirectory(cacheDirectory);

            string outputPath = Path.Combine(cacheDirectory, "preview_" + cacheKey + ".mp4");

            if (File.Exists(outputPath))
            {
                previewCache[cacheKey] = outputPath;
                return outputPath;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = ResolveFfmpegPath(),
                Arguments =
                    "-hide_banner -y -loglevel error " +
                    "-ss " + start.ToString(CultureInfo.InvariantCulture) + " " +
                    "-i " + QuoteArgument(videoPath) + " " +
                    "-t " + duration.ToString(CultureInfo.InvariantCulture) + " " +
                    "-map 0:v:0 -map 0:a? " +
                    "-c:v mpeg4 -q:v 6 " +
                    "-c:a aac -b:a 96k -movflags +faststart " +
                    QuoteArgument(outputPath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using Process process = new Process();
            process.StartInfo = startInfo;

            try
            {
                process.Start();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                throw new FileNotFoundException("FFmpeg를 실행할 수 없습니다.", ex);
            }

            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0 || !File.Exists(outputPath))
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "프리뷰 생성에 실패했습니다." : error.Trim());
            }

            previewCache[cacheKey] = outputPath;
            return outputPath;
        }

        private void RefreshMarkers()
        {
            markerBindingSource.ResetBindings(false);
        }

        private void SetAnalysisUi(bool enabled)
        {
            btnAnalyze.Enabled = enabled;
            btnLoadVideo.Enabled = enabled;
        }

        private void VideoMarkerAnalyzerForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Right && e.Control)
            {
                NextButton_Click(sender, e);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Left && e.Control)
            {
                PrevButton_Click(sender, e);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Space && e.Control)
            {
                AddManualButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private static string CreatePlayerHtml(string fileUrl)
        {
            string encodedUrl = JsonSerializer.Serialize(fileUrl, new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            return @"<!doctype html>
<html>
<head>
<meta charset=""utf-8"">
<style>
html, body { margin: 0; width: 100%; height: 100%; background: #111820; color: #d8e2e8; font-family: Arial, sans-serif; }
.wrap { width: 100%; height: 100%; display: flex; align-items: center; justify-content: center; }
video { width: 100%; height: 100%; background: #111820; }
.empty { color: #9fb0bb; font-size: 14px; }
</style>
</head>
<body>
<div class=""wrap"">
<video id=""video"" controls></video>
<div id=""empty"" class=""empty"">영상 파일을 불러와 주세요.</div>
</div>
<script>
const src = " + encodedUrl + @";
const video = document.getElementById('video');
const empty = document.getElementById('empty');
if (src) {
  video.src = src;
  video.style.display = 'block';
  empty.style.display = 'none';
} else {
  video.style.display = 'none';
  empty.style.display = 'block';
}
window.seekVideo = function(seconds) {
  if (!video.src) return;
  video.currentTime = Math.max(0, seconds || 0);
  video.focus();
};
window.getVideoTime = function() {
  return video.currentTime || 0;
};
</script>
</body>
</html>";
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static string ResolveFfmpegPath()
        {
            string baseDirectory = AppContext.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDirectory, "Tools", "ffmpeg.exe"),
                Path.Combine(baseDirectory, "ffmpeg.exe"),
                Path.Combine(Application.StartupPath, "Tools", "ffmpeg.exe"),
                Path.Combine(Application.StartupPath, "ffmpeg.exe")
            };

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            return "ffmpeg";
        }

        private static TableLayoutPanel CreateSidePanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 1;
            panel.RowCount = 5;
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, GetSideSectionOuterHeight(4)));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, GetSideSectionOuterHeight(2)));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, GetSideSectionOuterHeight(1)));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, GetSideSectionOuterHeight(1)));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.BackColor = UiTheme.AppBackColor;
            panel.Margin = Padding.Empty;
            return panel;
        }

        private static TableLayoutPanel CreateSideSection(string title, int rowCount)
        {
            TableLayoutPanel section = new TableLayoutPanel();
            section.Dock = DockStyle.Fill;
            section.ColumnCount = 1;
            section.RowCount = rowCount;
            section.Padding = new Padding(8, SideSectionPaddingTop, 8, SideSectionPaddingBottom);
            section.Margin = new Padding(0, 0, 0, SideSectionMarginBottom);
            section.BackColor = UiTheme.PanelBackColor;
            section.Paint += (s, e) =>
            {
                ControlPaint.DrawBorder(e.Graphics, section.ClientRectangle, UiTheme.BorderColor, ButtonBorderStyle.Solid);
            };

            section.RowStyles.Add(new RowStyle(SizeType.Absolute, SideSectionTitleHeight));
            for (int i = 1; i < rowCount; i++)
            {
                section.RowStyles.Add(new RowStyle(SizeType.Absolute, SideButtonRowHeight));
            }

            Label titleLabel = new Label();
            titleLabel.Dock = DockStyle.Fill;
            titleLabel.Text = title;
            titleLabel.ForeColor = UiTheme.TextColor;
            titleLabel.Font = UiTheme.BoldFont(10F);
            titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            section.Controls.Add(titleLabel, 0, 0);
            return section;
        }

        private static int GetSideSectionOuterHeight(int contentRowCount)
        {
            return SideSectionPaddingTop
                + SideSectionTitleHeight
                + contentRowCount * SideButtonRowHeight
                + SideSectionPaddingBottom
                + SideSectionMarginBottom
                + SideSectionGuard;
        }

        private static TableLayoutPanel CreateButtonGrid(int buttonCount)
        {
            TableLayoutPanel grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.ColumnCount = buttonCount * 2 - 1;
            grid.RowCount = 1;

            for (int i = 0; i < grid.ColumnCount; i++)
            {
                bool gapColumn = i % 2 == 1;
                grid.ColumnStyles.Add(gapColumn
                    ? new ColumnStyle(SizeType.Absolute, SideInnerGap)
                    : new ColumnStyle(SizeType.Percent, 100F / buttonCount));
            }

            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            grid.Margin = Padding.Empty;
            return grid;
        }

        private static void AddOptionControl(TableLayoutPanel grid, string labelText, Control control, int column, int row)
        {
            Label label = new Label();
            label.Dock = DockStyle.Fill;
            label.Text = labelText;
            label.ForeColor = UiTheme.MutedTextColor;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Font = UiTheme.RegularFont();
            label.Margin = Padding.Empty;

            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(0, 2, 8, 2);

            grid.Controls.Add(label, column, row);
            grid.Controls.Add(control, column + 1, row);
        }

        private static void ConfigurePanelButton(Button button, string text, Color backColor, Color foreColor)
        {
            UiTheme.StyleButton(button, backColor, foreColor);
            button.Text = text;
            button.Dock = DockStyle.Fill;
            button.Height = SideButtonHeight;
            button.Margin = new Padding(0, 2, 0, 4);
        }

        private static Control CreateToolbarSpacer()
        {
            Panel spacer = new Panel();
            spacer.Size = new Size(8, 28);
            spacer.Margin = new Padding(0, 0, 6, 6);
            return spacer;
        }

        private static FlowLayoutPanel CreateToolbarRow()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = false,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
        }

        private static void ConfigureButton(Button button, string text, Color backColor, Color foreColor, int width)
        {
            UiTheme.StyleToolbarButton(button, text, backColor, foreColor, width);
        }

        private static Label CreateToolbarLabel(string text, int width)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Size = new Size(width, 28),
                Margin = new Padding(0, 0, 3, 6),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = UiTheme.MutedTextColor
            };
        }

        private static void ConfigureNumber(NumericUpDown numeric, int minimum, int maximum, int value, int width)
        {
            numeric.Minimum = minimum;
            numeric.Maximum = maximum;
            numeric.Value = value;
            numeric.Size = new Size(width, 28);
            numeric.Margin = new Padding(0, 0, 6, 6);
            UiTheme.StyleNumeric(numeric);
        }

        private sealed class AudioWindow
        {
            public AudioWindow(int second, double rms, double peak)
            {
                Second = second;
                Rms = rms;
                Peak = peak;
            }

            public int Second { get; }
            public double Rms { get; }
            public double Peak { get; }
        }

        private sealed class AnalysisOptions
        {
            public double SpikeStdMultiplier { get; private set; }
            public double SpikeAverageMultiplier { get; private set; }
            public double ActiveStdMultiplier { get; private set; }
            public double ActiveAverageMultiplier { get; private set; }
            public double GrowthMultiplier { get; private set; }
            public int MaxCandidates { get; set; }
            public int MinGapSeconds { get; set; }

            public static AnalysisOptions CreateConservative()
            {
                return new AnalysisOptions
                {
                    SpikeStdMultiplier = 3.1,
                    SpikeAverageMultiplier = 4.2,
                    ActiveStdMultiplier = 1.15,
                    ActiveAverageMultiplier = 2.15,
                    GrowthMultiplier = 4.5,
                    MaxCandidates = 60,
                    MinGapSeconds = 45
                };
            }

            public static AnalysisOptions CreateBalanced()
            {
                return new AnalysisOptions
                {
                    SpikeStdMultiplier = 2.6,
                    SpikeAverageMultiplier = 3.4,
                    ActiveStdMultiplier = 0.9,
                    ActiveAverageMultiplier = 1.8,
                    GrowthMultiplier = 3.7,
                    MaxCandidates = 90,
                    MinGapSeconds = 30
                };
            }

            public static AnalysisOptions CreateSensitive()
            {
                return new AnalysisOptions
                {
                    SpikeStdMultiplier = 2.1,
                    SpikeAverageMultiplier = 2.7,
                    ActiveStdMultiplier = 0.7,
                    ActiveAverageMultiplier = 1.55,
                    GrowthMultiplier = 3.0,
                    MaxCandidates = 140,
                    MinGapSeconds = 18
                };
            }
        }

        private sealed class VideoMarkerCandidate
        {
            public int Seconds { get; set; }
            public string Kind { get; set; } = "";
            public int Score { get; set; }
            public string Status { get; set; } = "후보";
            public string Reason { get; set; } = "";
            public string Note { get; set; } = "";
            public string DisplayTime => TimeSpan.FromSeconds(Seconds).ToString(@"hh\:mm\:ss");
        }
    }
}
