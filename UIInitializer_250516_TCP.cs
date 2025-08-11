//using System;
//using System.Windows.Forms;

//namespace AmqpModbusIntegration
//{
//	public static class UIInitializer
//	{
//		// 初始化 ModbusViewer UI
//		public static void InitializeUI(
//			ModbusViewer viewer,
//			out TextBox stationNumberTextBox,
//			out Button connectButton,
//			out Button readButton,
//			out Button switchOnButton,
//			out Button switchOffButton,
//			out TextBox tempTextBox,
//			out Button tempSetButton,
//			out Button testFaultButton,
//			out Button refreshButton,
//			out DataGridView dataGridView)
//		{
//			viewer.Text = "PC Base Equipment Simulator";
//			viewer.Width = 900;
//			viewer.Height = 600;

//			// 連接按鈕
//			connectButton = new Button { Text = "連接", Location = new System.Drawing.Point(200, 10), Width = 80 };
//			viewer.Controls.Add(connectButton);

//			// 刷新按鈕
//			refreshButton = new Button { Text = "刷新 COM", Location = new System.Drawing.Point(300, 10), Width = 100 };
//			viewer.Controls.Add(refreshButton);

//			// 站號輸入
//			var stationLabel = new Label { Text = "站號:", Location = new System.Drawing.Point(10, 50), AutoSize = true };
//			viewer.Controls.Add(stationLabel);
//			stationNumberTextBox = new TextBox { Location = new System.Drawing.Point(70, 50), Width = 100 };
//			viewer.Controls.Add(stationNumberTextBox);

//			// 讀取數值
//			readButton = new Button { Text = "讀取數值", Location = new System.Drawing.Point(200, 50), Width = 80 };
//			viewer.Controls.Add(readButton);

//			// 開關控制
//			switchOnButton = new Button { Text = "合閘", Location = new System.Drawing.Point(300, 50), Width = 80 };
//			viewer.Controls.Add(switchOnButton);
//			switchOffButton = new Button { Text = "分閘", Location = new System.Drawing.Point(400, 50), Width = 80 };
//			viewer.Controls.Add(switchOffButton);

//			// 保護溫度設置
//			var tempLabel = new Label { Text = "保護溫度(℃):", Location = new System.Drawing.Point(500, 10), AutoSize = true };
//			viewer.Controls.Add(tempLabel);
//			tempTextBox = new TextBox { Location = new System.Drawing.Point(600, 10), Width = 100 };
//			viewer.Controls.Add(tempTextBox);
//			tempSetButton = new Button { Text = "設置", Location = new System.Drawing.Point(700, 10), Width = 80 };
//			viewer.Controls.Add(tempSetButton);

//			// 故障測試按鈕
//			testFaultButton = new Button { Text = "故障測試", Location = new System.Drawing.Point(500, 50), Width = 120 };
//			viewer.Controls.Add(testFaultButton);

//			// DataGridView
//			dataGridView = new DataGridView
//			{
//				Location = new System.Drawing.Point(10, 100),
//				Width = 850,
//				Height = 450,
//				AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
//				ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
//				AllowUserToAddRows = false,
//				ReadOnly = true
//			};
//			viewer.Controls.Add(dataGridView);
//		}
//	}
//}