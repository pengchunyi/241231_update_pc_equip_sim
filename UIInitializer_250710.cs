using System;
using System.IO.Ports;
using System.Windows.Forms;

namespace AmqpModbusIntegration
{
	public static class UIInitializer
	{

		/// <summary>
		/// 建立並回傳所有 UI 控制項（透過 out 參數）
		/// </summary>
		// 静态方法，用于初始化 ModbusViewer 的 UI
		public static void InitializeUI(
			ModbusViewer viewer,

			out ComboBox portSelector,
			out TextBox stationNumberTextBox,
			out Button connectButton,
			out Button readButton,
			out Button switchOnButton,

			out Button switchOffButton,
			out TextBox tempTextBox, // 20241129_新增
			out Button tempSetButton, // 20241129_新增
			out Button testFaultButton,
			out Button refreshPortButton, // 新增刷新按鈕

			out DataGridView dataGridView

			)
		{
			viewer.Text = "PC Base Equipment Simulator";
			viewer.Width = 900;
			viewer.Height = 600;

			//// COM口选择
			//var portLabel = new Label { Text = "COM口:", Location = new System.Drawing.Point(10, 10), AutoSize = true };
			//viewer.Controls.Add(portLabel);
			// === COM 來源清單 ===
			portSelector = new ComboBox
			{
				Location = new System.Drawing.Point(60, 10),
				Width = 120,
				DropDownStyle = ComboBoxStyle.DropDownList
			};
			portSelector.Items.AddRange(SerialPort.GetPortNames());
			if (portSelector.Items.Count > 0)
				portSelector.SelectedIndex = 0;
			viewer.Controls.Add(new Label { Text = "COM口:", Location = new System.Drawing.Point(10, 13) });
			viewer.Controls.Add(portSelector);



			// 连接按钮
			connectButton = new Button { Text = "連接", Location = new System.Drawing.Point(200, 10), Width = 80 };
			viewer.Controls.Add(connectButton);



			// 刷新COM口按钮
			refreshPortButton = new Button { Text = "刷新COM口", Location = new System.Drawing.Point(300, 10), Width = 100 };
			// 站号选择
			var stationNumberLabel = new Label { Text = "站號:", Location = new System.Drawing.Point(10, 50), AutoSize = true };
			stationNumberTextBox = new TextBox { Location = new System.Drawing.Point(70, 50), Width = 100 };
			// 读取数值按钮
			readButton = new Button { Text = "讀取數值", Location = new System.Drawing.Point(200, 50), Width = 80 };
			// 开关控制按钮 - 打开
			switchOnButton = new Button { Text = "合閘", Location = new System.Drawing.Point(300, 50), Width = 80 };
			// 开关控制按钮 - 关闭
			switchOffButton = new Button { Text = "分閘", Location = new System.Drawing.Point(400, 50), Width = 80 };
			//故障測試按鈕
			testFaultButton = new Button { Text = "故障測試", Location = new System.Drawing.Point(500, 50), Width = 120 };
			// 設置溫度功能的控件
			var tempLabel = new Label { Text = "保護溫度(℃):", Location = new System.Drawing.Point(500, 10), AutoSize = true }; // 放在測試故障右側
			tempTextBox = new TextBox { Location = new System.Drawing.Point(600, 10), Width = 100 }; // 放在標籤右側
			tempSetButton = new Button { Text = "設置", Location = new System.Drawing.Point(700, 10), Width = 80 }; // 放在文本框右側

			viewer.Controls.Add(refreshPortButton);
			viewer.Controls.Add(stationNumberLabel);
			viewer.Controls.Add(stationNumberTextBox);
			viewer.Controls.Add(readButton);
			viewer.Controls.Add(switchOnButton);
			viewer.Controls.Add(switchOffButton);
			viewer.Controls.Add(testFaultButton);
			viewer.Controls.Add(tempLabel);
			viewer.Controls.Add(tempTextBox);
			viewer.Controls.Add(tempSetButton);

			// 初始化 DataGridView
			dataGridView = new DataGridView
			{
				Location = new System.Drawing.Point(10, 100),
				Width = 850,
				Height = 400,
				AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
				ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
				AllowUserToAddRows = false, // 禁止用戶新增行
				ReadOnly = true             // 只讀模式
			};

			viewer.Controls.Add(dataGridView); // 将 DataGridView 添加到窗口控件
		}
	}
}
