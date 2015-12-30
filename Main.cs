using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Data.OleDb;
using System.IO;
using System.Linq;
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

namespace Concordance
{
    public partial class Main : Window
    {

        // CONSTANTS

        public const string SEARCH_MASK = "*-hepple.*";
        public const string ROOT_FOLDER = "D:\\Мама\\0. Люба\\7. Дисципліни-2014\\Дипломні роботи 4 курс\\Готові дипломні роботи\\OANC_GrAF\\OANC-GrAF\\data\\written_2\\technical";
        public const string CONNECTION_STRING = "Provider=Microsoft.ACE.OLEDB.12.0; Data Source=Database.accdb";

        // PRIVATE VARIABLES

        List<File> files = new List<File>();
        List<File> queue = new List<File>();
        List<string> soughtWords = new List<string>();
        List<Sequence> results = new List<Sequence>();
        ObservableCollection<Sequence> resultList = new ObservableCollection<Sequence>();
        OleDbConnection databaseConnection;
        File currentFile = null;
        Thread readThread = null;
        event BoolenAction OnFileProceeded;
        delegate bool BoolenAction();

        // INTERNAL FUNCTIONS

        public Main()
        {
            InitializeComponent();
            fileList.ItemsSource = this.files;
            queueList.ItemsSource = this.queue;
            resultsGrid.ItemsSource = resultList;
            ((INotifyCollectionChanged)queueList.Items).CollectionChanged += OnQueueSizeChanged;
            databaseConnection = new OleDbConnection(CONNECTION_STRING);
            databaseConnection.Open();
            OleDbCommand command = new OleDbCommand("select base from TagChain", databaseConnection);
            OleDbDataReader reader = command.ExecuteReader();
            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    soughtWords.Add(reader[0].ToString());
                }
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
            string commandText = "";
            char[] separators = new char[] { ' ', ',', '.', '!', '?', ';', '\n', '\t', '\r', ':', '-', '\"', '\'', '\\', '[', ']', '(', ')' };
            Sequence s = new Sequence();
            try
            {
                for (int i = 0; i < resultList.Count; i++)
                {
                    s = resultList[i];
                    s.Replace(separators);
                    commandText = "select id, frequency from Collocations where left1 like \"" + s.left1 + "\" and left0 like \"" + s.left0 + "\" and word like \"" + s.word + "\" and msd like \"" + s.msd + "\" and base like \"" + s.wordBase + "\" and right0 like \"" + s.right0 + "\" and right1 like \"" + s.right1 + "\"";
                    OleDbCommand command = new OleDbCommand(commandText, databaseConnection);
                    OleDbDataReader reader = command.ExecuteReader();
                    if (reader.HasRows)
                    {
                        reader.Read();
                        commandText = "update Collocations set frequency = " + (Convert.ToInt32(reader[1]) + 1) + " where id = " + reader[0].ToString() + "";
                        command = new OleDbCommand(commandText, databaseConnection);
                        reader = command.ExecuteReader();
                    }
                    else
                    {
                        commandText = "insert into Collocations (left1, left0, word, msd, base, right0, right1) values " + s.ToString();
                        command = new OleDbCommand(commandText, databaseConnection);
                        reader = command.ExecuteReader();
                    }
                }
            }
            catch (Exception exc)
            {
                System.Windows.Forms.MessageBox.Show(exc.Message);
                return false;
            }
            finally
            {
                OnFileProceeded = null;
            }
            resultList.Clear();
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
                                break;
                            }
                        }
                    }
                    index = -1;
                }
                Dispatcher.Invoke(() =>
                {
                    f.state = FileStates.SUCCEED;
                    currentFile = null;
                    if (autoMoveBox.IsChecked == true)
                    {
                        if (!MoveToDatabase())
                        {
                            f.state = FileStates.ERROR;
                        }
                    }
                    queueList.Items.Refresh();
                    if (OnFileProceeded != null)
                    {
                        OnFileProceeded();
                    }
                    OnQueueSizeChanged(null, null);
                });
            }
            catch (Exception exc)
            {
                System.Windows.Forms.MessageBox.Show(exc.Message + exc.StackTrace);
                f.state = FileStates.ERROR;
                queueList.Items.Refresh();
                currentFile = null;
                readThread.Abort();
            }
        }

        private Sequence ProceedSubfile(string filename, string baseWord, int start, int end)
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

        // EVENT CALLBACKS

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
                fileInfoLabel.Content = String.Format("File quantity: {0};", files.Count);
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
                readThread.Priority = ThreadPriority.BelowNormal;
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
    }
}
