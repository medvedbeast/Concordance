using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Data;
using System.Data.OleDb;
using System.Collections.ObjectModel;

namespace DatabaseFormer
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static string connectionString = @"Provider=Microsoft.ACE.OLEDB.12.0; Data Source=Database.accdb";
        OleDbConnection connection;

        List<string> filePaths = new List<string>();
        ObservableCollection<Concordance> resultList = new ObservableCollection<Concordance>();

        public static int currentFileCount = 0;
        public static int targetFileCount = 0;

        public MainWindow()
        {
            InitializeComponent();
            Title = "Database Former v.1";
            this.Closing += MainWindow_Closing;
            collocationGrid.ItemsSource = resultList;
        }

        void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            targetFileCount = 0;
            fileCycleCheckBox.IsChecked = false;
        }

        public void ConnectDB(string connectionString)
        {
            connection = new OleDbConnection(connectionString);
            connection.Open();
        }

        public void DisconnectDB()
        {
            if (connection.State == ConnectionState.Open)
            {
                connection.Close();
            }
        }

        public void FindInXml(string fileName, int fileNamePosition)
        {
            StreamReader reader = new StreamReader(fileName);
            string file = reader.ReadToEnd();
            reader.Close();
            string[] strings = file.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            file = null;

            string[] parts;
            string word = "";
            string msd = "";
            int wordPosition = -1;

            for (int i = 0; i < strings.Length; i++)
            {
                if (Regex.IsMatch(strings[i], "<f name=\"base\" value=\"[a-zA-Z0-9]*\"/>"))
                {
                    parts = strings[i].Split(new char[] { '\"' }, StringSplitOptions.RemoveEmptyEntries);
                    word = parts[parts.Length - 2];
                    msd = "";
                    wordPosition = -1;
                    foreach (var item in wordListBox.Items)
                    {
                        if (item.ToString() == word)
                        {
                            bool flag = false;
                            if (Regex.IsMatch(strings[i - 1], "<f name=\"msd\" value=\"[a-zA-Z0-9]*\"/>"))
                            {
                                parts = strings[i - 1].Split(new char[] { '\"' }, StringSplitOptions.RemoveEmptyEntries);
                                msd = parts[parts.Length - 2];
                                flag = true;
                            }
                            else if (Regex.IsMatch(strings[i + 1], "<f name=\"msd\" value=\"[a-zA-Z0-9]*\"/>"))
                            {
                                parts = strings[i + 1].Split(new char[] { '\"' });
                                msd = parts[parts.Length - 2];
                                flag = true;
                            }
                            if (flag)
                            {
                                int cursor = i;
                                while (wordPosition == -1)
                                {
                                    if (Regex.IsMatch(strings[cursor], "<region xml:id=\"penn-[a-zA-Z0-9]*\" anchors=\"[0-9]* [0-9]*\"/>"))
                                    {
                                        parts = strings[cursor].Split(new char[] { ' ', '\"' }, StringSplitOptions.RemoveEmptyEntries);
                                        wordPosition = Convert.ToInt32(parts[parts.Length - 3]);
                                    }
                                    else
                                    {
                                        cursor--;
                                    }
                                }
                                List<string> concordance = new List<string>();
                                concordance.AddRange(FindInTxt(fileName, wordPosition));
                                concordance.Add(word);
                                concordance.Add(msd);
                                Dispatcher.Invoke(
                                    new Action(() =>
                                    {
                                        resultList.Add(new Concordance(concordance.ToArray()));
                                    }),
                                    System.Windows.Threading.DispatcherPriority.Background
                                    );
                            }
                        }
                    }
                }
            }
            ListBoxItem listItem = new ListBoxItem();
            listItem.Content = fileListBox.Items[fileNamePosition];
            listItem.Background = Brushes.Green;
            fileListBox.Items[fileNamePosition] = listItem;
            if (currentFileCount == targetFileCount)
            {
                if (fileCycleCheckBox.IsChecked == true)
                {
                    currentFileCount = 0;
                    updateDatabaseButton_Click_1(null, null);
                }
                else
                {
                    fileCountBox.IsEnabled = true;
                    MessageBox.Show(targetFileCount + " processed succesfully!");
                }
            }
            
        }

        public string[] FindInTxt(string fileName, int position)
        {
            string[] left = new string[] { "", "" };
            string[] right = new string[] { "", "" };
            string word = "";
            string tmp = fileName.Substring(0, fileName.Length - 11);
            fileName = tmp;
            fileName += ".txt";
            StreamReader reader = new StreamReader(fileName);
            string file = reader.ReadToEnd();
            reader.Close();
            reader.Dispose();
            char[] separators = new char[] { ' ', ',', '.', '!', '?', ';', '\n' };
            int spacePosition = position;
            while (!separators.Contains<char>(file[spacePosition]))
            {
                word += file[spacePosition];
                spacePosition++;
            }

                
            int leftPosition = position - 2;
            int count = 0;
            while (count < 2)
            {
                if (!separators.Contains<char>(file[leftPosition]))
                {
                    left[count] += file[leftPosition];
                }
                else
                {
                    char[] array = left[count].ToCharArray();
                    Array.Reverse(array);
                    left[count] = new string(array);
                    count++;
                }
                leftPosition--;
            }

                
            int rightPosition = spacePosition + 1;
            count = 0;
            while (count < 2)
            {
                if (!separators.Contains<char>(file[rightPosition]))
                {
                    right[count] += file[rightPosition];
                }
                else
                {
                    count++;
                }
                rightPosition++;
            }
            return new string[] { left[1], left[0], word, right[0], right[1] };
        }

        public void GetData()
        {
            OleDbCommand command = new OleDbCommand("select base from TagChain", connection);
            OleDbDataReader reader = command.ExecuteReader();
            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    wordListBox.Items.Add(reader[0].ToString());
                }
            }
        }

        public void WriteData()
        {
            Concordance c = new Concordance();
            try
            {
                foreach (var item in resultList)
                {
                    c = item;
                    string commandText = "select id, frequency from Collocations where left1 like \"" + item.left1 + "\" and left0 like \"" + item.left0 + "\" and word like \"" + item.word + "\" and msd like \"" + item.msd + "\" and base like \"" + item.wordBase + "\" and right0 like \"" + item.right0 + "\" and right1 like \"" + item.right1 + "\"";
                    OleDbCommand command = new OleDbCommand(commandText, connection);
                    OleDbDataReader reader = command.ExecuteReader();
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            commandText = "update Collocations set frequency = " + (Convert.ToInt32(reader[1]) + 1) + " where id = " + reader[0].ToString() + "";
                            command = new OleDbCommand(commandText, connection);
                            reader = command.ExecuteReader();
                        }
                    }
                    else
                    {
                        commandText = "insert into Collocations (left1, left0, word, msd, base, right0, right1) values " + item.ToString();
                        command = new OleDbCommand(commandText, connection);
                        reader = command.ExecuteReader();
                    }
                }
                connection.Close();
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message + exception.StackTrace + c);
            }
        }

        private void updateDatabaseButton_Click_1(object sender, RoutedEventArgs e)
        {
            ConnectDB(connectionString);
            WriteData();
            DisconnectDB();
            resultList.Clear();
        }

        private void getWordsButton_Click_1(object sender, RoutedEventArgs e)
        {
            ConnectDB(connectionString);
            GetData();
            DisconnectDB();
            fileListBox.IsEnabled = true;
            processButton.IsEnabled = true;
        }

        private void fileSearchButton_Click_1(object sender, RoutedEventArgs e)
        {
            if (filePathBox.Text == "")
            {
                MessageBox.Show("Type filename!");
                return;
            }
            filePaths.AddRange(Directory.GetFileSystemEntries(filePathBox.Text, "*-hepple.xml", SearchOption.AllDirectories));
            foreach (var item in filePaths)
            {
                string[] items = item.Split(new char[] { '\\' });
                fileListBox.Items.Add(items[items.Length - 1]);
            }
            fileCountBox.Text = fileListBox.Items.Count.ToString();
        }

        private void processButton_Click_1(object sender, RoutedEventArgs e)
        {
            fileCountBox.IsEnabled = false;
            targetFileCount = Convert.ToInt32(fileCountBox.Text);
            Dispatcher.Invoke(new Action(() =>
                {
                    currentFileCount = 0;
                    for (int i = 0; i < filePaths.Count; i++)
                    {
                        if (currentFileCount < targetFileCount)
                        {
                            ListBoxItem item = fileListBox.ItemContainerGenerator.ContainerFromItem(fileListBox.Items[i]) as ListBoxItem;
                            if (item.Background != Brushes.Green)
                            {
                                currentFileCount++;
                                FindInXml(filePaths[i], i);
                            }
                        }
                        else
                        {
                            return;
                        }
                    }
                }),
                System.Windows.Threading.DispatcherPriority.Background
                );
        }

        private void fileListBox_MouseDoubleClick_1(object sender, MouseButtonEventArgs e)
        {
            if (fileListBox.SelectedIndex != -1)
            {
                ConfirmationBox confirmationWindow = new ConfirmationBox();
                confirmationWindow.ShowDialog();
                if (confirmationWindow.result)
                {
                    FindInXml(filePaths[fileListBox.SelectedIndex], fileListBox.SelectedIndex);
                }
            }
        }

        
    }
}
