using System;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;

namespace AmqpModbusIntegration
{
	public static class UIInitializer
	{

		public static void InitializeUI(
			ModbusViewer viewer,
			out ComboBox portSelector,
			out TextBox stationNumberTextBox,
			out Button connectButton,
			out Button readButton,
			out Button switchOnButton,
			out Button switchOffButton,
			out TextBox tempTextBox,
			out Button tempSetButton,
			out Button testFaultButton,
			out Button refreshPortButton,
			out DataGridView dataGridView,
			out ListView iniListView,
			out TextBox logTextBox
		)
		{
			// === 窗口基本屬性 ===
			viewer.Text = "PC Base Equipment Monitor";
			viewer.Width = 1200;
			viewer.Height = 750;
			viewer.FormBorderStyle = FormBorderStyle.Sizable;
			viewer.MaximizeBox = true;
			viewer.MinimizeBox = true;
			viewer.AutoSize = false;
			viewer.AutoSizeMode = AutoSizeMode.GrowAndShrink;

			// =============== 上方控制列 ===============
			var portLabel = new Label { Text = "COM口:", Location = new Point(10, 10), AutoSize = true };
			viewer.Controls.Add(portLabel);

			portSelector = new ComboBox
			{
				Location = new Point(70, 10),
				Width = 120,
				DropDownStyle = ComboBoxStyle.DropDownList
			};
			portSelector.Items.AddRange(SerialPort.GetPortNames());
			if (portSelector.Items.Count > 0)
				portSelector.SelectedIndex = 0;
			viewer.Controls.Add(portSelector);

			connectButton = new Button { Text = "連接", Location = new Point(200, 10), Width = 80 };
			viewer.Controls.Add(connectButton);

			refreshPortButton = new Button { Text = "刷新COM口", Location = new Point(300, 10), Width = 100 };
			viewer.Controls.Add(refreshPortButton);

			// =============== 第二行：站號與控制 ===============
			var stationNumberLabel = new Label { Text = "站號:", Location = new Point(10, 50), AutoSize = true };
			viewer.Controls.Add(stationNumberLabel);

			stationNumberTextBox = new TextBox { Location = new Point(70, 50), Width = 100 };
			viewer.Controls.Add(stationNumberTextBox);

			readButton = new Button { Text = "讀取數值", Location = new Point(200, 50), Width = 80 };
			viewer.Controls.Add(readButton);

			switchOnButton = new Button { Text = "合閘", Location = new Point(300, 50), Width = 80 };
			viewer.Controls.Add(switchOnButton);

			switchOffButton = new Button { Text = "分閘", Location = new Point(400, 50), Width = 80 };
			viewer.Controls.Add(switchOffButton);

			testFaultButton = new Button { Text = "故障測試", Location = new Point(500, 50), Width = 120 };
			viewer.Controls.Add(testFaultButton);

			// =============== 右側溫度設定 ===============
			var tempLabel = new Label { Text = "保護溫度(℃):", Location = new Point(500, 10), AutoSize = true };
			viewer.Controls.Add(tempLabel);

			tempTextBox = new TextBox { Location = new Point(600, 10), Width = 100 };
			viewer.Controls.Add(tempTextBox);

			tempSetButton = new Button { Text = "設置", Location = new Point(700, 10), Width = 80 };
			viewer.Controls.Add(tempSetButton);

			// =============== DataGridView ===============
			dataGridView = new DataGridView
			{
				Location = new Point(10, 80),
				Width = 850,
				Height = 320,
				AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
				ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
				AllowUserToAddRows = false,
				ReadOnly = true,
				Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
			};
			viewer.Controls.Add(dataGridView);

			// =============== INI ListView（右側） ===============

			// 先在方法開頭宣告欄位參數，不用 ref/out 傳進 lambda
			ListView localIniListView;


			iniListView = new ListView
			{
				View = View.Details,
				Location = new Point(880, 10),
				Width = 300,
				Height = 400,
				GridLines = true,
				FullRowSelect = true,
				Anchor = AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom,
			};

			iniListView.Columns.Add("Key", 140);
			iniListView.Columns.Add("Value", 140);
			viewer.Controls.Add(iniListView);

			// 將 out 參數指派到本地變數，給下面函數用
			localIniListView = iniListView;

			// === 動態調整欄寬 ===
			void ResizeIniColumns(object sender, EventArgs e)
			{
				int filler = localIniListView.ClientSize.Width
							 - localIniListView.Columns[0].Width
							 - SystemInformation.VerticalScrollBarWidth
							 - 4;
				if (filler < 50) filler = 50;
				localIniListView.Columns[1].Width = filler;
			}
			localIniListView.Resize += ResizeIniColumns;
			ResizeIniColumns(null!, EventArgs.Empty);

			viewer.Controls.Add(iniListView);





			// =============== LOG 區（底部） ===============
			logTextBox = new TextBox
			{
				Location = new Point(10, 420),
				Width = 1170,
				Height = 270,
				Multiline = true,
				ScrollBars = ScrollBars.Vertical,
				ReadOnly = true,
				BackColor = Color.Black,
				ForeColor = Color.Lime,
				Font = new Font("Consolas", 9F),
				Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
			};
			viewer.Controls.Add(logTextBox);


		}

	}



}
