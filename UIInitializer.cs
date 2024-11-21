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
            out DataGridView dataGridView)
        {
            viewer.Text = "Modbus Control";
            viewer.Width = 900;
            viewer.Height = 600;

            // COM口选择
            var portLabel = new Label { Text = "COM口:", Location = new System.Drawing.Point(10, 10), AutoSize = true };
            viewer.Controls.Add(portLabel);
            portSelector = new ComboBox { Location = new System.Drawing.Point(70, 10), Width = 100 };
            portSelector.Items.AddRange(SerialPort.GetPortNames());
            viewer.Controls.Add(portSelector);

            // 站号选择
            var stationNumberLabel = new Label { Text = "站號:", Location = new System.Drawing.Point(10, 50), AutoSize = true };
            viewer.Controls.Add(stationNumberLabel);
            stationNumberTextBox = new TextBox { Location = new System.Drawing.Point(70, 50), Width = 100 };
            viewer.Controls.Add(stationNumberTextBox);

            // 连接按钮
            connectButton = new Button { Text = "連接", Location = new System.Drawing.Point(200, 10), Width = 80 };
            viewer.Controls.Add(connectButton);

            // 读取数值按钮
            readButton = new Button { Text = "讀取數值", Location = new System.Drawing.Point(200, 50), Width = 80 };
            viewer.Controls.Add(readButton);

            // 开关控制按钮 - 打开
            switchOnButton = new Button { Text = "合閘", Location = new System.Drawing.Point(300, 50), Width = 80 };
            viewer.Controls.Add(switchOnButton);

            // 开关控制按钮 - 关闭
            switchOffButton = new Button { Text = "分閘", Location = new System.Drawing.Point(400, 50), Width = 80 };
            viewer.Controls.Add(switchOffButton);

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
