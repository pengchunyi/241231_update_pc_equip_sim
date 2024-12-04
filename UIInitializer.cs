using System;
using System.IO.Ports;
using System.Windows.Forms;

namespace AmqpModbusIntegration
{
    public static class UIInitializer
    {
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

			out DataGridView dataGridView)
        {
            viewer.Text = "Modbus Control";
            viewer.Width = 900;
            viewer.Height = 600;

            // COM口选择
            var portLabel = new Label { Text = "COM口:", Location = new System.Drawing.Point(10, 10), AutoSize = true };
            viewer.Controls.Add(portLabel);

			// 连接按钮
			connectButton = new Button { Text = "連接", Location = new System.Drawing.Point(200, 10), Width = 80 };
			viewer.Controls.Add(connectButton);

			portSelector = new ComboBox { Location = new System.Drawing.Point(70, 10), Width = 100 };

			//20241125新增
			// **添加初始化時立即刷新COM口的邏輯**
			portSelector.Items.Clear();
			portSelector.Items.AddRange(SerialPort.GetPortNames());
            viewer.Controls.Add(portSelector);

			// 刷新COM口按钮
			refreshPortButton = new Button { Text = "刷新COM口", Location = new System.Drawing.Point(300, 10), Width = 100 };
			viewer.Controls.Add(refreshPortButton);

			// 站号选择
			var stationNumberLabel = new Label { Text = "站號:", Location = new System.Drawing.Point(10, 50), AutoSize = true };
            viewer.Controls.Add(stationNumberLabel);

            stationNumberTextBox = new TextBox { Location = new System.Drawing.Point(70, 50), Width = 100 };
            viewer.Controls.Add(stationNumberTextBox);



            // 读取数值按钮
            readButton = new Button { Text = "讀取數值", Location = new System.Drawing.Point(200, 50), Width = 80 };
            viewer.Controls.Add(readButton);

            // 开关控制按钮 - 打开
            switchOnButton = new Button { Text = "合閘", Location = new System.Drawing.Point(300, 50), Width = 80 };
            viewer.Controls.Add(switchOnButton);

            // 开关控制按钮 - 关闭
            switchOffButton = new Button { Text = "分閘", Location = new System.Drawing.Point(400, 50), Width = 80 };
            viewer.Controls.Add(switchOffButton);

			//20241127_新增====================================================
			// 新增測試按鈕
			testFaultButton = new Button { Text = "故障測試", Location = new System.Drawing.Point(500, 50), Width = 120 };
			viewer.Controls.Add(testFaultButton);
			//20241127_新增====================================================



			//20241129_新增===================================================
			// 設置溫度功能的控件
			var tempLabel = new Label { Text = "保護溫度(℃):", Location = new System.Drawing.Point(500, 10), AutoSize = true }; // 放在測試故障右側
			viewer.Controls.Add(tempLabel);

			tempTextBox = new TextBox { Location = new System.Drawing.Point(600, 10), Width = 100 }; // 放在標籤右側
			viewer.Controls.Add(tempTextBox);

			tempSetButton = new Button { Text = "設置", Location = new System.Drawing.Point(700, 10), Width = 80 }; // 放在文本框右側
			viewer.Controls.Add(tempSetButton);

			// 將新增控件加入參數列表（視需要決定是否添加到返回值）
			//20241129_新增===================================================



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
