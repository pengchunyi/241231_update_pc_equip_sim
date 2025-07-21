//using System;
//using System.Drawing;
//using System.IO.Ports;
//using System.Windows.Forms;

//namespace AmqpModbusIntegration
//{
//	public static class UIInitializer
//	{
//		// 静态方法，用于初始化 ModbusViewer 的 UI
//		//public static void InitializeUI(
//		//	ModbusViewer viewer,
//		//	out ComboBox portSelector,
//		//	out TextBox stationNumberTextBox,
//		//	out Button connectButton,
//		//	out Button readButton,

//		//	out Button switchOnButton,
//		//	out Button switchOffButton,
//		//	out TextBox tempTextBox, // 20241129_新增
//		//	out Button tempSetButton, // 20241129_新增
//		//	out Button testFaultButton,

//		//	out Button refreshPortButton, // 新增刷新按鈕
//		//	out DataGridView dataGridView,


//		//		/* 新增 OUT 參數 */
//		//	out ListView iniListView,
//		//	out TextBox logTextBox
//		//	)
//		//{
//		//	viewer.Text = "PC Base Equipment Monitor";
//		//	viewer.Width = 900;
//		//	viewer.Height = 600;



//		//	// =============== 上方控制列 ===============
//		//	// COM口选择
//		//	var portLabel = new Label { Text = "COM口:", Location = new System.Drawing.Point(10, 10), AutoSize = true };
//		//	viewer.Controls.Add(portLabel);


//		//	portSelector = new ComboBox // <--- 加這一段
//		//	{
//		//		Location = new System.Drawing.Point(70, 10),
//		//		Width = 120,
//		//		DropDownStyle = ComboBoxStyle.DropDownList // 只能選不能輸入
//		//	};
//		//	portSelector.Items.AddRange(SerialPort.GetPortNames());
//		//	if (portSelector.Items.Count > 0)
//		//		portSelector.SelectedIndex = 0;
//		//	viewer.Controls.Add(portSelector);


//		//	// 连接按钮
//		//	connectButton = new Button { Text = "連接", Location = new System.Drawing.Point(200, 10), Width = 80 };
//		//	viewer.Controls.Add(connectButton);



//		//	// 刷新COM口按钮
//		//	refreshPortButton = new Button { Text = "刷新COM口", Location = new System.Drawing.Point(300, 10), Width = 100 };
//		//	viewer.Controls.Add(refreshPortButton);



//		//	// =============== 第二行：站號與控制 ===============
//		//	// 站号选择
//		//	var stationNumberLabel = new Label { Text = "站號:", Location = new System.Drawing.Point(10, 50), AutoSize = true };
//		//	viewer.Controls.Add(stationNumberLabel);

//		//	stationNumberTextBox = new TextBox { Location = new System.Drawing.Point(70, 50), Width = 100 };
//		//	viewer.Controls.Add(stationNumberTextBox);

//		//	// 读取数值按钮
//		//	readButton = new Button { Text = "讀取數值", Location = new System.Drawing.Point(200, 50), Width = 80 };
//		//	viewer.Controls.Add(readButton);

//		//	// 开关控制按钮 - 打开
//		//	switchOnButton = new Button { Text = "合閘", Location = new System.Drawing.Point(300, 50), Width = 80 };
//		//	viewer.Controls.Add(switchOnButton);

//		//	// 开关控制按钮 - 关闭
//		//	switchOffButton = new Button { Text = "分閘", Location = new System.Drawing.Point(400, 50), Width = 80 };
//		//	viewer.Controls.Add(switchOffButton);

//		//	// 新增測試按鈕
//		//	testFaultButton = new Button { Text = "故障測試", Location = new System.Drawing.Point(500, 50), Width = 120 };
//		//	viewer.Controls.Add(testFaultButton);



//		//	// =============== 溫度設定 ===============
//		//	// 設置溫度功能的控件
//		//	var tempLabel = new Label { Text = "保護溫度(℃):", Location = new System.Drawing.Point(500, 10), AutoSize = true }; // 放在測試故障右側
//		//	viewer.Controls.Add(tempLabel);

//		//	tempTextBox = new TextBox { Location = new System.Drawing.Point(600, 10), Width = 100 }; // 放在標籤右側
//		//	viewer.Controls.Add(tempTextBox);

//		//	tempSetButton = new Button { Text = "設置", Location = new System.Drawing.Point(700, 10), Width = 80 }; // 放在文本框右側
//		//	viewer.Controls.Add(tempSetButton);

//		//	// =============== 數據窗口顯示===============
//		//	// 初始化 DataGridView
//		//	dataGridView = new DataGridView
//		//	{
//		//		Location = new System.Drawing.Point(10, 100),
//		//		Width = 850,
//		//		Height = 400,
//		//		AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
//		//		ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
//		//		AllowUserToAddRows = false, // 禁止用戶新增行
//		//		ReadOnly = true             // 只讀模式
//		//	};

//		//	viewer.Controls.Add(dataGridView); // 将 DataGridView 添加到窗口控件



//		//	//----------------------------------------------------------------
//		//	// INI 參數區 (右側)
//		//	//----------------------------------------------------------------
//		//	iniListView = new ListView
//		//	{
//		//		View = View.Details,
//		//		Location = new Point(880, 10),
//		//		Width = 300,
//		//		Height = 510,
//		//		GridLines = true,
//		//		FullRowSelect = true,
//		//	};
//		//	iniListView.Columns.Add("Key", 140);
//		//	iniListView.Columns.Add("Value", 140);
//		//	viewer.Controls.Add(iniListView);

//		//	//----------------------------------------------------------------
//		//	// CFX Log 區 (底部)
//		//	//----------------------------------------------------------------
//		//	logTextBox = new TextBox
//		//	{
//		//		Location = new Point(10, 560),
//		//		Width = 1170,
//		//		Height = 140,
//		//		Multiline = true,
//		//		ScrollBars = ScrollBars.Vertical,
//		//		ReadOnly = true,
//		//		BackColor = Color.Black,
//		//		ForeColor = Color.Lime,
//		//		Font = new Font("Consolas", 9F)
//		//	};
//		//	viewer.Controls.Add(logTextBox);

//		//	// 視窗拉大一點
//		//	viewer.Width = 1200;
//		//	viewer.Height = 750;

//		//}


//		public static void InitializeUI(
//	ModbusViewer viewer,
//	out ComboBox portSelector,
//	out TextBox stationNumberTextBox,
//	out Button connectButton,
//	out Button readButton,
//	out Button switchOnButton,
//	out Button switchOffButton,
//	out TextBox tempTextBox,
//	out Button tempSetButton,
//	out Button testFaultButton,
//	out Button refreshPortButton,
//	out DataGridView dataGridView,
//	out ListView iniListView,
//	out TextBox logTextBox
//)
//		{
//			// === 窗口基本屬性 ===
//			viewer.Text = "PC Base Equipment Monitor";
//			viewer.Width = 1200;
//			viewer.Height = 750;
//			viewer.FormBorderStyle = FormBorderStyle.Sizable;
//			viewer.MaximizeBox = true;
//			viewer.MinimizeBox = true;
//			viewer.AutoSize = false;
//			viewer.AutoSizeMode = AutoSizeMode.GrowAndShrink;

//			// =============== 上方控制列 ===============
//			var portLabel = new Label { Text = "COM口:", Location = new Point(10, 10), AutoSize = true };
//			viewer.Controls.Add(portLabel);

//			portSelector = new ComboBox
//			{
//				Location = new Point(70, 10),
//				Width = 120,
//				DropDownStyle = ComboBoxStyle.DropDownList
//			};
//			portSelector.Items.AddRange(SerialPort.GetPortNames());
//			if (portSelector.Items.Count > 0)
//				portSelector.SelectedIndex = 0;
//			viewer.Controls.Add(portSelector);

//			connectButton = new Button { Text = "連接", Location = new Point(200, 10), Width = 80 };
//			viewer.Controls.Add(connectButton);

//			refreshPortButton = new Button { Text = "刷新COM口", Location = new Point(300, 10), Width = 100 };
//			viewer.Controls.Add(refreshPortButton);

//			// =============== 第二行：站號與控制 ===============
//			var stationNumberLabel = new Label { Text = "站號:", Location = new Point(10, 50), AutoSize = true };
//			viewer.Controls.Add(stationNumberLabel);

//			stationNumberTextBox = new TextBox { Location = new Point(70, 50), Width = 100 };
//			viewer.Controls.Add(stationNumberTextBox);

//			readButton = new Button { Text = "讀取數值", Location = new Point(200, 50), Width = 80 };
//			viewer.Controls.Add(readButton);

//			switchOnButton = new Button { Text = "合閘", Location = new Point(300, 50), Width = 80 };
//			viewer.Controls.Add(switchOnButton);

//			switchOffButton = new Button { Text = "分閘", Location = new Point(400, 50), Width = 80 };
//			viewer.Controls.Add(switchOffButton);

//			testFaultButton = new Button { Text = "故障測試", Location = new Point(500, 50), Width = 120 };
//			viewer.Controls.Add(testFaultButton);

//			// =============== 溫度設定 ===============
//			var tempLabel = new Label { Text = "保護溫度(℃):", Location = new Point(500, 10), AutoSize = true };
//			viewer.Controls.Add(tempLabel);

//			tempTextBox = new TextBox { Location = new Point(600, 10), Width = 100 };
//			viewer.Controls.Add(tempTextBox);

//			tempSetButton = new Button { Text = "設置", Location = new Point(700, 10), Width = 80 };
//			viewer.Controls.Add(tempSetButton);

//			// =============== DataGridView ===============
//			dataGridView = new DataGridView
//			{
//				Location = new Point(10, 100),
//				Width = 850,
//				Height = 400,
//				AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
//				ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
//				AllowUserToAddRows = false,
//				ReadOnly = true,
//				Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
//			};
//			viewer.Controls.Add(dataGridView);

//			// =============== INI ListView（右側） ===============
//			iniListView = new ListView
//			{
//				View = View.Details,
//				Location = new Point(880, 10),
//				Width = 300,
//				Height = 510,
//				GridLines = true,
//				FullRowSelect = true,
//				Anchor = AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom,


//				HeaderStyle = ColumnHeaderStyle.Clickable,
//				OwnerDraw = false
//			};

//			iniListView.Columns.Add("Key", 140);
//			iniListView.Columns.Add("Value", 140);

//			viewer.Controls.Add(iniListView);

//			// =============== LOG 區（底部） ===============
//			logTextBox = new TextBox
//			{
//				Location = new Point(10, 560),
//				Width = 1170,
//				Height = 140,
//				Multiline = true,
//				ScrollBars = ScrollBars.Vertical,
//				ReadOnly = true,
//				BackColor = Color.Black,
//				ForeColor = Color.Lime,
//				Font = new Font("Consolas", 9F),
//				Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
//			};
//			viewer.Controls.Add(logTextBox);


//		}

//	}



//}
