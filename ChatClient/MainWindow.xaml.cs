﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Newtonsoft.Json;
using WebSocketSharp;

namespace ChatClient
{
	/// <summary>
	/// Логика взаимодействия для TestWindow.xaml
	/// </summary>
	public partial class MainWindow: Window
	{
		public MainWindow()
		{
			InitializeComponent();
		}

		/// <summary>
		/// return center position 
		/// </summary>
		/// <param name="resolution">is the full length of pixels</param>
		/// <param name="actual">size of app in pixels</param>
		private double getCenter(double resolution, double actual)
		{
			return (resolution - actual) / 2;
		}

		/// <summary>
		/// runs after rendering window; 
		/// send request for all channels 
		/// </summary>
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			// center Window after loaded
			this.Top = getCenter(SystemParameters.PrimaryScreenHeight, ActualHeight);
			this.Left = getCenter(SystemParameters.PrimaryScreenWidth, ActualWidth);

			// отправляем запрос всех доступных каналов
			// возвращается массив объектов {name, fullname, admin}
			var ws = wsController.getWs();
			if (ws != null)
			{
				var getChannels = new ChannelRequest();
				getChannels.type = "get_channel";
				getChannels.name = "*";
				getChannels.from = Config.userName;
				string getAllCh = JsonConvert.SerializeObject(getChannels);
				ws.Send(getAllCh);
			}
		}


		private string currentChannel;
		// using when send user's messages
		private string userIdentification = "Me";
		
		private WsController wsController;
		private FileLogger l = new FileLogger(Config.logFileName);

		public void setWsController(WsController c)
		{
			c.setMainWindow(this);
			wsController = c;
		}

		/// <summary>
		/// create channelGrid with selected params;
		/// other params are getted from pattern;
		/// </summary>
		private Grid createChannelGrid(string fullname, int newM, int users, string name)
		{
			var g1 = GenericsWPF<Grid>.DeepDarkCopy(ChannelSampleGrid);
			g1.Visibility = Visibility.Visible;
			var ch = g1.Children;
			((TextBlock)ch[0]).Text = fullname;
			((TextBlock)ch[1]).Text = "" + newM;
			((TextBlock)ch[2]).Text = users + " участников";
			((TextBlock)ch[3]).Text = name;
			return g1;
		}

		// used for counting created channels
		private int indx = 1;

		/// <summary>
		/// press the button create new channel:
		/// it send request for creation a channel, 
		/// and if response.success == true, it show the channel
		/// <see cref="createChannel"/>
		/// </summary>
		private void EllipseChannels_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			// создаем новый канал
			string name = "sampleCh" + indx;
			string fullname = "канал " + indx;

			// отправляем запрос о создании канала
			var ws = wsController.getWs();
			if (ws != null)
			{
				NewChannelRequest r = new NewChannelRequest();
				r.type = "new_channel";
				r.name = name;
				r.fullname = fullname;
				r.admin = Config.userName;
				ws.Send(JsonConvert.SerializeObject(r));
			}
			indx++;
		}


		/// <summary>
		/// low level method, for create message view
		/// <see cref="createChannelGrid"/>
		/// </summary>
		/// <returns>grid object with needed parameters</returns>
		private Grid createMessageGrid(string name, string message, string time, Grid obj)
		{
			var g1 = GenericsWPF<Grid>.DeepDarkCopy(obj);
			g1.Visibility = Visibility.Visible;
			var ch = g1.Children;
			((TextBlock)ch[0]).Text = name;
			((TextBlock)ch[1]).Text = message;
			((TextBlock)ch[2]).Text = time;
			return g1;
		}

		/// <summary>
		/// shortcat for <see cref="createMessageGrid"/> for current user's messages
		/// </summary>
		private Grid createMyMessageGrid(string message, string time)
		{
			return createMessageGrid(userIdentification, message, time, MyMessageGrid);
		}
		/// <summary>
		/// shortcat for <see cref="createMessageGrid"/> for another users messages
		/// </summary>
		private Grid createAnotherMessageGrid(string name, string message, string time)
		{
			return createMessageGrid(name, message, time, AnotherMessageGrid);
		}

		/// <summary>
		/// we retrieve long time from server and view it in message grids
		/// </summary>
		private string longToDateTime(long time)
		{
			return new DateTime(time*10000).ToString(@"dd\.MM HH\:mm");
		}

		/// <summary>
		/// current date and time
		/// </summary>
		private string getCurrentDatenTime()
		{
			return DateTime.Now.ToString(@"dd\.MM HH\:mm");
		}
		/// <summary>
		/// current time
		/// </summary>
		private string getCurrentTime()
		{
			return DateTime.Now.ToString(@"HH\:mm");
		}

		/// <summary>
		/// scrolls messageList, used after sending (and receiving (?)) messages
		/// </summary>
		private void ScrollMessageListToEnd()
		{
			MessageList.ScrollIntoView(MessageList.Items[MessageList.Items.Count - 1]);
		}

		/// <summary>
		/// on 'Enter' key method sends message
		/// </summary>
		private void TextBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				if (MessageTextBox.Text != "")
				{
					var ws = wsController.getWs();
					if (ws != null)
					{
						// берем имя выбранного канала
						Grid ch = (Grid)ChannelList.SelectedItems[0];
						string name = ((TextBlock)ch.Children[3]).Text;

						string time = getCurrentTime();
						MessageList.Items.Add(createMyMessageGrid(MessageTextBox.Text, time));
						MessageResponse mes = new MessageResponse("message", MessageTextBox.Text, Config.userName, DateTimeOffset.Now.ToUnixTimeMilliseconds(), name);
						string jsonReq = JsonConvert.SerializeObject(mes);
						l.log("sending message");
						ws.Send(jsonReq);

						MessageTextBox.Text = "";
						ScrollMessageListToEnd();
					}
				}
			}
		}

		// serialisation methods
		private string WriteFromArrayOfObjectsToJson(MessageBean[] beans)
		{
			return JsonConvert.SerializeObject(beans);
		}
		private MessageBean MessageGridToObject(Grid messageGrid)
		{
			var ch = messageGrid.Children;
			string sender =  ((TextBlock)ch[0]).Text;
			string message = ((TextBlock)ch[1]).Text;
			string time =    ((TextBlock)ch[2]).Text;
			return new MessageBean(sender, message, time);
		}
		private MessageBean[] MessageListGridsToObjectArray()
		{
			int count = MessageList.Items.Count;
			MessageBean[] beans = new MessageBean[count];
			for (int i = 0; i < count; i++)
			{
				MessageBean b = MessageGridToObject((Grid)MessageList.Items[i]);
				beans[i] = b;
			}
			return beans;
		}


		public void dispatchCreateChannel(NewChannelResponse m)
		{
			Dispatcher.BeginInvoke(new ThreadStart(delegate {
				createChannel(m);
			}));
		}
		
		/// <summary>
		/// retrieve new loaded channel
		/// </summary>
		private void createChannel(NewChannelResponse m)
		{
			ChannelList.Items.Add(createChannelGrid(m.fullname, 2 + indx, 5 + indx, m.name));
		}


		public void dispatchShowChannels(List<dynamic> channels)
		{
			Dispatcher.BeginInvoke(new ThreadStart(delegate {
				showChannels(channels);
			}));
		}
		
		/// <summary>
		/// add channels from server to ChannelList
		/// </summary>
		private void showChannels(List<dynamic> channels)
		{

			foreach (dynamic ch in channels)
			{
				string n = ch.name;
				string fn = ch.fullname;
				Grid g = createChannelGrid(fn, 0, 0, n);
				ChannelList.Items.Add(g);
			}
		}


		public void dispatchShowMessage(MessageResponse mes)
		{
			Dispatcher.BeginInvoke(new ThreadStart(delegate {
				showMessage(mes);
			}));
		}
		/// <summary>
		/// show new messages from server for current channel
		/// </summary>
		private void showMessage(MessageResponse mes)
		{
			Grid ch = (Grid)ChannelList.SelectedItems[0];
			string name = ((TextBlock)ch.Children[3]).Text;
			if (mes.channel == name)
			{
				Grid mGrid;
				if (mes.@from.Equals(Config.userName))
				{
					mGrid = createMyMessageGrid(mes.message, getCurrentTime());
				}
				else
				{
					mGrid = createAnotherMessageGrid(mes.@from, mes.message, getCurrentTime());
				}
				MessageList.Items.Add(mGrid);
				ScrollMessageListToEnd();
			}
			// TODO: сериализация сообщений
		}


		public void dispatchShowChannelMessages(string channelName, List<dynamic> messages)
		{
			Dispatcher.BeginInvoke(new ThreadStart(delegate {
				showChannelMessages(channelName, messages);
			}));
		}

		/// <summary>
		/// display channel messages from server, if channel is chosen
		/// </summary>
		/// <param name="channelName"></param>
		/// <param name="messages"></param>
		public void showChannelMessages(string channelName, List<dynamic> messages)
		{
			// смотрим, какой канал сейчас выбран
			Grid ch = (Grid) ChannelList.SelectedItems[0];
			string name = ((TextBlock)ch.Children[3]).Text;
			if (channelName == name)
			{
				MessageList.Items.Clear();
				// рендерим список сообщений
				foreach (dynamic m in messages)
				{
					string mes = m.message;
					string fr = m.from;
					string channel = m.channel;
					long time = m.time;
					string type = m.type;
					// TODO: сериализовывать сообщения
					Grid mGrid;
					if (fr.Equals(Config.userName))
					{
						mGrid = createMyMessageGrid(mes, longToDateTime(time));
					}
					else
					{
						mGrid = createAnotherMessageGrid(fr, mes, longToDateTime(time));
					}
					MessageList.Items.Add(mGrid);
					ScrollMessageListToEnd();
				}
			}
			// TODO: сериализация
			// если нет, то сохраняем в стеш (пока что дропаем)
		}


		private void EllipseMessage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			// TODO: пофиксить, тестовая сериализация
			MessageBean[] beans = MessageListGridsToObjectArray();
			string beansJ = WriteFromArrayOfObjectsToJson(beans);
			File.WriteAllText("MessageList.json", beansJ);

		}

		// if ChannelGrid state is Collapsed
		private bool isCollapsed = false;

		/// <summary>
		/// button hide channel list
		/// </summary>
		private void BackRect_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (!SetChannelGridVisibilityCollapsed())
			{
				SetChannelGridVisibilityVisible();
			}
		}

		/// <returns>true if successfully change statement</returns>
		private bool SetChannelGridVisibilityCollapsed()
		{
			if (!isCollapsed)
			{
				ChannelGrid.Visibility = Visibility.Collapsed;
				isCollapsed = true;
				return true;
			}
			return false;
		}
		
		/// <returns>true if successfully change statement</returns>
		private bool SetChannelGridVisibilityVisible()
		{
			if (isCollapsed)
			{
				ChannelGrid.Visibility = Visibility.Visible;
				isCollapsed = false;
				return true;
			}
			return false;
		}
		
		/// <summary>
		/// hide channel list
		/// </summary>
		private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
		{
				if (e.NewSize.Width <= 800)
				{
					SetChannelGridVisibilityCollapsed();
				}
				// > 800
				else
				{
					SetChannelGridVisibilityVisible();
				}
		}


		/// <summary>
		/// change chatHeaders name and count of members from parameters text variables
		/// </summary>
		private void changeHeader(TextBlock nameBlockFrom, TextBlock countBlockFrom) 
		{
			TextBlock chatNameBlock = (TextBlock)ChatHeader.Children[0];
			TextBlock chatCountBlock = (TextBlock)ChatHeader.Children[1];

			chatNameBlock.Text = nameBlockFrom.Text;
			chatCountBlock.Text = countBlockFrom.Text;
		}

		/// <summary>
		/// method change at least one parameter, or throw exception
		/// </summary>
		/// <param name="targetChannel">cannot be null, throw exception</param>
		/// <param name="channelName"></param>
		/// <param name="channelNew"></param>
		/// <param name="channelCount"></param>
		private void changeChannelParams(Grid targetChannel, string channelName, string channelNew, string channelCount)
		{
			int i = 0;
			if (targetChannel != null)
			{
				var ch = targetChannel.Children;
				if (channelName != null)
				{
					((TextBlock) ch[0]).Text = channelName;
					i++;
				}
				if (channelNew!= null)
				{
					((TextBlock)ch[1]).Text = channelNew;
					i++;
				}
				if (channelCount != null)
				{
					((TextBlock)ch[2]).Text = channelCount;
					i++;
				}

				if (i == 0)
				{
					throw new  Exception("all grid's params are null");
				}
			}
			else
			{
				throw new Exception("target cannot be null");
			}
		}


		/// <summary>
		/// changed selection
		/// </summary>
		private void ChannelList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			Grid channel = (Grid) e.AddedItems[0];
			TextBlock nameBlock = (TextBlock)channel.Children[0];
			TextBlock countBlock = (TextBlock)channel.Children[2];
			changeHeader(nameBlock, countBlock);

			// меняем шапку
			// очищаем messageList (и сохраняем messageList иного канала)
			// загружаем историю сообщений для данного канала
			// отправляем запрос на получение истории данного канала
			string chName = ((TextBlock)channel.Children[3]).Text;
			var ws = wsController.getWs();
			if (ws != null)
			{
				var getChannelMessages = new GetChannelMessagesReq();
				getChannelMessages.type = "get_channel_messages";
				getChannelMessages.channel = chName;
				getChannelMessages.from = Config.userName;
				string getChM = JsonConvert.SerializeObject(getChannelMessages);
				ws.Send(getChM);
			}
		}

	}
}
