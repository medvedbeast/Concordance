using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace Concordance
{
    public partial class Main : Window
    {

        // CONSTANTS

        public const string SEARCH_MASK = "*-hepple.*";
        public const string ROOT_FOLDER = "D:\\Мама\\0. Люба\\7. Дисципліни-2014\\Дипломні роботи 4 курс\\Готові дипломні роботи\\OANC_GrAF\\OANC-GrAF\\data";
        public const string CONNECTION_STRING = "Server=127.0.0.1;Database=concordance;Uid=root;";

        // PRIVATE VARIABLES

        List<File> files = new List<File>();
        List<File> queue = new List<File>();
        List<string> soughtWords = new List<string>();
        List<Sequence> results = new List<Sequence>();
        ObservableCollection<Sequence> resultList = new ObservableCollection<Sequence>();
        MySqlConnection databaseConnection;
        File currentFile = null;
        Thread readThread = null;
        int frequencyIndex = -1;
        event Action OnFileProceeded;
        delegate bool Action();


        // INTERNAL FUNCTIONS

        public Main()
        {
            try
            {
                InitializeComponent();
                fileList.ItemsSource = this.files;
                queueList.ItemsSource = this.queue;
                resultsGrid.ItemsSource = resultList;
                ((INotifyCollectionChanged)queueList.Items).CollectionChanged += OnQueueSizeChanged;
                databaseConnection = new MySqlConnection(CONNECTION_STRING);
                databaseConnection.Open();
                MySqlCommand command = new MySqlCommand("select base from Source", databaseConnection);
                MySqlDataReader reader = command.ExecuteReader();
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        soughtWords.Add(reader[0].ToString());
                    }
                }
                reader.Close();
                reader.Dispose();
                databaseConnection.Close();
            }
            catch (Exception exc)
            {
                Log(exc.Message, exc.StackTrace);
                return;
            }

        }

        private void GetFiles(List<File> list, string startPath)
        {
            string[] directories = Directory.GetDirectories(startPath);
            foreach (string item in directories)
            {
                GetFiles(list, item);
            }
            string[] files = Directory.GetFiles(startPath, SEARCH_MASK);
            foreach (string item in files)
            {
                list.Add(new File(item));
            }
        }

        private bool MoveToDatabase()
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            if (databaseConnection.State != System.Data.ConnectionState.Open || databaseConnection == null)
            {
                databaseConnection = new MySqlConnection(CONNECTION_STRING);
                databaseConnection.Open();
            }
            char[] separators = new char[] { ' ', ',', '.', '!', '?', ';', '\n', '\t', '\r', ':', '-', '\"', '\'', '\\', '[', ']', '(', ')' };
            Sequence s = new Sequence();
            try
            {
                MySqlCommand command;
                int count = 0;
                for (int i = 0; i < resultList.Count; i++, count++)
                {
                    s = resultList[i];
                    s.Replace(separators);

                    command = new MySqlCommand(string.Format("insert into Raw (left1, left0, word, base, msd, right0, right1) values ('{0}', '{1}', '{2}', '{3}', '{4}', '{5}', '{6}');", s.left1, s.left0, s.word, s.wordBase, s.msd, s.right0, s.right1), databaseConnection);
                    command.ExecuteNonQuery();

                    SetResultsInfoText(i);
                }
            }
            catch (Exception exc)
            {
                Dispatcher.Invoke(() =>
                {
                    Log(currentFile.fullPath, exc.Message, exc.StackTrace);
                    System.Windows.Forms.MessageBox.Show(exc.Message);
                });
                return false;
            }
            finally
            {
                Dispatcher.Invoke(() =>
                {
                    watch.Stop();
                    LogFormat("\n{0}\n\nProceeded {1} rows in {2:f6} seconds (~{3:f5}ms per row)", currentFile.fullPath, resultList.Count, (float)watch.ElapsedMilliseconds / 1000, (float)watch.ElapsedMilliseconds / (resultList.Count == 0 ? 1 : resultList.Count));
                    ClearResultsInfoText();
                    OnFileProceeded = null;
                    resultList.Clear();
                });
            }
            return true;
        }

        private void ProceedFile(File f, int fileIndex)
        {
            f.state = FileStates.IN_USE;
            ListBoxItem item = queueList.ItemContainerGenerator.ContainerFromItem(queueList.Items[fileIndex]) as ListBoxItem;
            try
            {
                StreamReader reader = new StreamReader(f.fullPath);
                string file = reader.ReadToEnd();
                string[] strings = file.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                file = "";
                int index = 0;
                for (int i = 0; i < strings.Length; i++)
                {
                    if (strings[i].IndexOf("base") > 0)
                    {
                        for (int j = 0; j < soughtWords.Count; j++)
                        {
                            index = strings[i].IndexOf("\"" + soughtWords[j] + "\"");
                            if (index > 0)
                            {
                                int start = 0;
                                int end = 0;
                                int position = 0;

                                int tmpIndex = i;
                                string soughtLine = "";
                                while (soughtLine.IndexOf("region") <= 0)
                                {
                                    tmpIndex--;
                                    soughtLine = strings[tmpIndex];
                                }

                                string tmp;
                                string baseWord;
                                position = strings[i].IndexOf("value");
                                tmp = strings[i].Substring(position + 7, strings[i].Length - position - 7);
                                baseWord = tmp.Substring(0, tmp.IndexOf('\"'));
                                position = strings[tmpIndex].IndexOf("anchors");
                                tmp = strings[tmpIndex].Substring(position + 9);
                                start = int.Parse(tmp.Substring(0, tmp.IndexOf(' ')));
                                tmp = tmp.Substring(tmp.IndexOf(' ') + 1);
                                end = int.Parse(tmp.Substring(0, tmp.IndexOf('\"')));
                                Sequence s = ProceedSubfile(f.GetSubfile(), baseWord, start, end);
                                if (s != null)
                                {
                                    soughtLine = "";
                                    while (soughtLine.IndexOf("msd") <= 0)
                                    {
                                        tmpIndex++;
                                        soughtLine = strings[tmpIndex];
                                    }
                                    position = strings[tmpIndex].IndexOf("value");
                                    tmp = strings[tmpIndex].Substring(position + 7, strings[tmpIndex].Length - position - 7);
                                    s.msd = tmp.Substring(0, tmp.IndexOf('\"'));

                                    Dispatcher.Invoke(() => { resultList.Add(s); });
                                    SetResultsInfoText();
                                }
                                break;
                            }
                        }
                    }
                    index = -1;
                }
                f.state = FileStates.SUCCEED;

                bool flag = false;
                Dispatcher.Invoke(() => { flag = (autoMoveBox.IsChecked == true ? true : false); });
                if (flag)
                {
                    if (!MoveToDatabase())
                    {
                        f.state = FileStates.ERROR;
                    }
                }
                currentFile = null;
                Dispatcher.Invoke(() => { queueList.Items.Refresh(); });
                if (OnFileProceeded != null)
                {
                    OnFileProceeded();
                }
                OnQueueSizeChanged(null, null);
            }
            catch (Exception exc)
            {
                Dispatcher.Invoke(() =>
                {
                    System.Windows.Forms.MessageBox.Show(exc.Message + exc.StackTrace);
                    f.state = FileStates.ERROR;
                    queueList.Items.Refresh();
                    currentFile = null;
                    readThread.Abort();
                    Log(f.fullPath, exc.Message, exc.StackTrace);
                });

            }
        }

        private Sequence ProceedSubfile(string filename, string baseWord, int start, int end)
        {
            try
            {
                int length = end - start;
                StreamReader reader = new StreamReader(filename);
                string file = reader.ReadToEnd();
                int fStart = 0;
                int fEnd = 0;
                fStart = start - 100 <= 0 ? -1 * (100 - start) : start - 100;
                fEnd = start + 100 > file.Length ? fEnd = file.Length - start : fEnd = start + 100;
                file = file.Substring(fStart, fEnd - fStart);
                start -= fStart;
                end = start + length;
                char[] separators = new char[] { ' ', ',', '.', '!', '?', ';', '\n', '\t', '\r', ':', '-' };
                Sequence sequence = new Sequence();
                sequence.wordBase = baseWord;
                sequence.word = file.Substring(start, length);
                int leftPosition = start - 2;
                bool separator = false;
                while (!separator)
                {
                    for (int i = 0; i < separators.Length; i++)
                    {
                        if (file[leftPosition] == separators[i])
                        {
                            separator = true;
                            break;
                        }
                    }
                    if (!separator)
                    {
                        leftPosition--;
                    }
                    if (leftPosition - 1 < 0)
                    {
                        break;
                    }
                }
                sequence.left0 = file.Substring(leftPosition + 1, start - leftPosition - 2);
                leftPosition -= 1;
                separator = false;
                while (!separator)
                {
                    for (int i = 0; i < separators.Length; i++)
                    {
                        if (file[leftPosition] == separators[i])
                        {
                            separator = true;
                            break;
                        }
                    }
                    if (!separator)
                    {
                        leftPosition--;
                    }
                    if (leftPosition - 1 < 0)
                    {
                        break;
                    }
                }
                sequence.left1 = file.Substring(leftPosition + 1, start - leftPosition - sequence.left0.Length - 3);
                int rightPosition = end + 2;
                separator = false;
                while (!separator)
                {
                    for (int i = 0; i < separators.Length; i++)
                    {
                        if (file[rightPosition] == separators[i])
                        {
                            separator = true;
                            break;
                        }
                    }
                    if (!separator)
                    {
                        rightPosition++;
                    }
                    if (rightPosition + 1 >= file.Length)
                    {
                        break;
                    }
                }
                sequence.right0 = file.Substring(end + 1, rightPosition - end - 1);
                rightPosition += 1;
                separator = false;
                while (!separator)
                {
                    for (int i = 0; i < separators.Length; i++)
                    {
                        if (file[rightPosition] == separators[i])
                        {
                            separator = true;
                            break;
                        }
                    }
                    if (!separator)
                    {
                        rightPosition++;
                    }
                    if (rightPosition + 1 >= file.Length)
                    {
                        break;
                    }
                }
                sequence.right1 = file.Substring(end + sequence.right0.Length + 2, rightPosition - end - sequence.right0.Length - 2);
                return sequence;
            }
            catch (Exception)
            {

            }
            return null;
        }

        private void SetInfoText()
        {
            fileInfoLabel.Content = String.Format("File quantity: {0}; Selected file #: {1}.", files.Count, fileList.SelectedIndex);
        }

        private void ClearResultsInfoText()
        {
            Dispatcher.Invoke(() =>
            {
                resultsStatus.Content = "";
            });
        }

        private void SetResultsInfoText(int loaded = 0)
        {
            Dispatcher.Invoke(() =>
            {
                int items = resultList.Count;
                resultsStatus.Content = string.Format("Rows: {0}; Loaded to database: {1} ", items, loaded);
            });
        }

        private void Log(params object[] message)
        {
            StreamWriter writer = new StreamWriter("output.log", true, System.Text.Encoding.UTF8);
            writer.WriteLine("/------------------------/\n" + DateTime.Now);
            for (int i = 0; i < message.Length; i++)
            {
                writer.Write(message[i] + "\n" + (i == message.Length - 1 ? "" : "\n"));
            }
            writer.WriteLine();
            writer.Flush();
            writer.Close();
        }

        private void LogFormat(string format, params object[] message)
        {
            StreamWriter writer = new StreamWriter("output.log", true, System.Text.Encoding.UTF8);
            writer.WriteLine("/------------------------/\n" + DateTime.Now);
            writer.WriteLine(format, message);
            writer.WriteLine();
            writer.Flush();
            writer.Close();
        }

        // EVENT CALLBACKS

        private void OnFileListSelection(object sender, SelectionChangedEventArgs e)
        {
            SetInfoText();
        }

        private void OnSelectPathClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
                dialog.SelectedPath = ROOT_FOLDER;
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                if (result.ToString() != "OK")
                {
                    return;
                }
                string[] parts = dialog.SelectedPath.Split('\\');
                pathField.Content = dialog.SelectedPath;
                List<File> files = new List<File>();
                GetFiles(files, dialog.SelectedPath);
                this.files = files;
                fileList.ItemsSource = this.files;
            }
            catch (Exception) { }
        }

        private void OnMoveRightClicked(object sender, RoutedEventArgs e)
        {
            int index = 0;
            List<File> toDelete = new List<File>();
            for (int i = 0; i < fileList.SelectedItems.Count; i++)
            {
                index = fileList.Items.IndexOf(fileList.SelectedItems[i]);
                queue.Add(files[index]);
                toDelete.Add(files[index]);
            }
            for (int i = 0; i < toDelete.Count; i++)
            {
                files.Remove(toDelete[i]);
            }
            fileList.SelectedIndex = -1;
            fileList.Items.Refresh();
            queueList.Items.Refresh();

        }

        private void OnMoveLeftClicked(object sender, RoutedEventArgs e)
        {
            int index = 0;
            List<File> toDelete = new List<File>();
            for (int i = 0; i < queueList.SelectedItems.Count; i++)
            {
                index = queueList.Items.IndexOf(queueList.SelectedItems[i]);
                if (files[index].state == FileStates.NONE)
                {
                    files.Add(queue[index]);
                    toDelete.Add(queue[index]);
                }
            }
            for (int i = 0; i < toDelete.Count; i++)
            {
                queue.Remove(toDelete[i]);
            }
            queueList.SelectedIndex = -1;
            fileList.Items.Refresh();
            queueList.Items.Refresh();
        }

        private void OnMoveAllRightClicked(object sender, RoutedEventArgs e)
        {
            queue.AddRange(files);
            files.Clear();
            fileList.Items.Refresh();
            queueList.Items.Refresh();
            queueList.SelectedIndex = -1;
            fileList.SelectedIndex = -1;
        }

        private void OnMoveAllLeftClicked(object sender, RoutedEventArgs e)
        {
            List<File> tmp = new List<File>();
            for (int i = 0; i < queue.Count; i++)
            {
                if (queue[i].state == FileStates.NONE)
                {
                    tmp.Add(queue[i]);
                    queue.RemoveAt(i);
                }
            }
            files.AddRange(tmp);
            fileList.Items.Refresh();
            queueList.Items.Refresh();
            queueList.SelectedIndex = -1;
            fileList.SelectedIndex = -1;
        }

        private void OnQueueSizeChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (OnFileProceeded != null && currentFile == null)
            {
                OnFileProceeded = null;
            }
            if (currentFile == null && queue.Count > 0)
            {
                int index = -1;
                for (int i = 0; i < queue.Count; i++)
                {
                    if (queue[i].state == FileStates.NONE)
                    {
                        index = i;
                        break;
                    }
                }
                if (index == -1)
                {
                    return;
                }
                currentFile = queue[index];
                readThread = new Thread(() =>
                {
                    ProceedFile(queue[index], index);
                });
                readThread.Priority = ThreadPriority.Highest;
                readThread.Start();
            }
        }

        private void OnMoveToDatabaseClicked(object sender, RoutedEventArgs e)
        {
            if (readThread.IsAlive)
            {
                OnFileProceeded += MoveToDatabase;
            }
            else
            {
                MoveToDatabase();
            }
        }

        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (currentFile != null)
            {
                LogFormat("{0}\n\nWindow closed", currentFile.fullPath);
            }
            if (frequencyIndex != -1)
            {
                LogFormat("Frequency creation interrupted on line {0}\n\nWindow closed", frequencyIndex);
            }
        }

        private void OnCreateFrequencyClicked(object sender, RoutedEventArgs e)
        {


            string separator = "/--------------------------------------------------------------/\n";

            Thread t = new Thread(() =>
            {
                try
                {
                    int count = -1;
                    int startIndex = -1;
                    Dispatcher.Invoke(() => { startIndex = int.Parse(line.Text); });

                    databaseConnection = new MySqlConnection(CONNECTION_STRING);
                    databaseConnection.Open();


                    MySqlCommand command = new MySqlCommand("select count(id) from Raw", databaseConnection);
                    count = Convert.ToInt32(command.ExecuteScalar());

                    Dispatcher.Invoke(() => { line.Text = startIndex + " / " + count; });


                    if (count == -1)
                    {
                        return;
                    }

                    MySqlDataReader reader;
                    for (int i = startIndex; i < count; i++)
                    {
                        frequencyIndex = i;
                        Sequence s = new Sequence();
                        command = new MySqlCommand("select left1, left0, word, base, msd, right0, right1 from Raw limit " + i + ", 1;", databaseConnection);
                        reader = command.ExecuteReader();
                        while (reader.Read())
                        {
                            s.left1 = (string)reader[0];
                            s.left0 = (string)reader[1];
                            s.word = (string)reader[2];
                            s.wordBase =  (string)reader[3];
                            s.msd = (string)reader[4];
                            s.right0 = (string)reader[5];
                            s.right1 = (string)reader[6];
                        }
                        reader.Close();

                        string table = char.ToUpper(s.wordBase[0]) + s.wordBase.Substring(1);

                        command = new MySqlCommand();
                        command.Connection = databaseConnection;
                        command.CommandType = System.Data.CommandType.StoredProcedure;
                        command.CommandText = "InsertCollocation";
                        command.Parameters.AddWithValue("tableName", table);
                        command.Parameters.AddWithValue("@left1", s.left1);
                        command.Parameters.AddWithValue("@left0", s.left0);
                        command.Parameters.AddWithValue("@word", s.word);
                        command.Parameters.AddWithValue("@base", s.wordBase);
                        command.Parameters.AddWithValue("@msd", s.msd);
                        command.Parameters.AddWithValue("@right0", s.right0);
                        command.Parameters.AddWithValue("@right1", s.right1);
                        command.ExecuteNonQuery();

                        Dispatcher.Invoke(() => { info.Text = string.Format("Progress: {0, 10} / {1, 10}", i, count); });
                    }

                    Dispatcher.Invoke(() => { info.Text += "\nCompleted!"; });

                }
                catch (Exception exc)
                {
                    Dispatcher.Invoke(() => { info.Text += separator + "\n" + exc.Message + "\n" + exc.StackTrace + "\n" + separator; });
                    LogFormat("Frequency creation interrupted on line {0}\n{1}\n{2}\n", frequencyIndex, exc.Message, exc.StackTrace);
                }
                return;
            });
            t.Start();

        }
    }
}
