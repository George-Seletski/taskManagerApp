using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Management;
using Microsoft.VisualBasic;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Runtime.ConstrainedExecution;
using System.Security;
using System.Threading;

namespace TaskManager
{
    public partial class Form1 : Form
    {
        //Загружаем обновленный список процессов. Снимок процессов на текущий момент после обновления.
        private List<Process> _processes = null;
        //Экземпляр списка для сортировки.
        private ListViewItemComparer comparer = null;

        public Form1()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
        }
        //Заполнение списка(работает только с бэкендом,не взаим. с интерфейсом)
        private void GetProcesses() 
        {
            _processes.Clear();
            _processes = Process.GetProcesses().ToList<Process>();
        }
        //Уже взаим. с интерфейсом

        string BasetPriorityToPriortyString(int basePriority)
        {
            switch (basePriority)
            {
                case 0:
                    return "Системный";
                case 4:
                    return "Низкий";
                case 6:
                    return "Ниже среднего";
                case 8:
                    return "Нормальный";
                case 9:
                    return "Обычный";
                case 10:
                    return "Выше среднего";
                case 13:
                    return "Высокий";
                case 24:
                    return "Реального времени";
                default:
                    return "Неизветно";
            }
        }

        CancellationTokenSource cancellation = null;

        async Task LoadMem(Process process, ListViewItem item, CancellationToken cancellation = default)
        {
            await Task.Factory.StartNew(() =>
            {
                try
                {
                    PerformanceCounter pc = new PerformanceCounter("Process", "Working Set - Private", process.ProcessName);
                    double memSize = 0;
                    if (!cancellation.IsCancellationRequested)
                        memSize = (double)pc.NextValue() / 1024;

                    if (!cancellation.IsCancellationRequested)
                        item.SubItems[1] = new ListViewItem.ListViewSubItem(item, Math.Round(memSize, 1).ToString() + " К");
                    pc.Close();
                }
                catch { }
            });
        }


        private void RefreshProcessesList()
        {
            cancellation?.Cancel();
            cancellation = new CancellationTokenSource();

            toolStripButton1.Enabled = false;

            listView1.Items.Clear();

            var processes = new List<ListViewItem>();

            foreach (Process p in _processes)
            {

                //Массив строк в колонки

                string[] row = new string[] { p.ProcessName.ToString(), "...", p.Id.ToString(), p.BasePriority.ToString(), p.ProcessName.ToString(), BasetPriorityToPriortyString(p.BasePriority) };

                var item = new ListViewItem(row);

                var loadMemTask = LoadMem(p, item,  cancellation.Token);

                processes.Add(item);


                //   pc.Dispose();
            }

            toolStripButton1.Enabled = true;

            listView1.Items.AddRange(processes.ToArray());

            Text = "Запущено процессов: " + processes.Count.ToString();

        }

        //Перегрузка метода(т.к. есть поле с фильтром для поиска по имени)
        private void RefreshProcessesList(List<Process> processes,string keyword)
        {
            try
            {
                double memSize = 0;
                listView1.Items.Clear();

                foreach (Process p in processes)
                {

                    if (p != null)
                    {
                        memSize = 0;

                        PerformanceCounter pc = new PerformanceCounter();
                        pc.CategoryName = "Process";
                        pc.CounterName = "Working Set - Private";
                        pc.InstanceName = p.ProcessName;
                        
                        memSize = (double)pc.NextValue() / (1000 * 1000);

                        string[] row = new string[] { p.ProcessName.ToString(), Math.Round(memSize, 1).ToString(), p.ProcessName.ToString() };

                        listView1.Items.Add(new ListViewItem(row));

                        pc.Close();
                        pc.Dispose();
                    }
                }
                Text = $"Запущено процессов '{keyword}':" + processes.Count.ToString();
            }
            catch (Exception) { }
        }

        //Завершить один процесс
        private void KillProcess(Process process) 
        {
            process.Kill();
            process.WaitForExit();
        }

        //Полностью завершает процесс и связанные с ним дочерние процессы
        private void KillProcessAndChildren(int pid) 
        {
            if (pid == 0) 
            {
                return;
            }
            //Находит по айдишнику процессы и по очереди рекурсивно все завершаются
            ManagementObjectSearcher searcher = new ManagementObjectSearcher
                ("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection objectCollection = searcher.Get();

            foreach(ManagementObject obj in objectCollection) 
            {
                KillProcessAndChildren(Convert.ToInt32(obj["ProcessID"]));
            }

            try
            {
                Process p = Process.GetProcessById(pid);
                p.Kill();
                p.WaitForExit();
            }

            catch (ArgumentException) { }
        }
        //Получение ID родительского процесса
        private int GetParentProcessId(Process p) 
        {
            int parentID = 0;

            try 
            {
                ManagementObject managementObject = new ManagementObject("win32_process.handle='" + p.Id + "'");

                managementObject.Get();

                parentID = Convert.ToInt32(managementObject["ParentProcessID"]);
            }
            catch (Exception) { }

            return parentID;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            _processes = new List<Process>();

            GetProcesses();

            RefreshProcessesList();

            comparer = new ListViewItemComparer();
            comparer.ColumnIndex = 0;
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            GetProcesses();

            RefreshProcessesList();
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            try 
            {
                if (listView1.SelectedItems[0] != null) 
                {
                    //Сравниваем имя каждого процесса с текстом в крайней колонке
                    Process processToKill = _processes.Where((x) => x.ProcessName ==
                    listView1.SelectedItems[0].SubItems[0].Text).ToList()[0];

                    KillProcess(processToKill);

                    GetProcesses();

                    RefreshProcessesList();

                }
            }
            catch (Exception) { }
        }

        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            try
            {
                if (listView1.SelectedItems[0] != null)
                {
                    Process processToKill = _processes.Where((x) => x.ProcessName ==
                    listView1.SelectedItems[0].SubItems[0].Text).ToList()[0];

                    KillProcessAndChildren(GetParentProcessId(processToKill));

                    GetProcesses();

                    RefreshProcessesList();
                }
            }
            catch (Exception) { }
        }

        private void завершитьДеревоПроцессовToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (listView1.SelectedItems[0] != null)
                {
                    Process processToKill = _processes.Where((x) => x.ProcessName ==
                    listView1.SelectedItems[0].SubItems[0].Text).ToList()[0];

                    KillProcessAndChildren(GetParentProcessId(processToKill));

                    GetProcesses();

                    RefreshProcessesList();
                }
            }
            catch (Exception) { }
        }


        private void запуститьЗадачуToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = Interaction.InputBox("Введите имя программы", "Запуск новой задачи");

            try
            {
                Process.Start(path);
            }
            catch (Exception) { }

        }


        private void toolStripTextBox1_TextChanged(object sender, EventArgs e)
        {
            GetProcesses();

            List<Process> filteredprocesses = _processes.Where((x) => 
            x.ProcessName.ToLower().Contains(toolStripTextBox1.Text.ToLower())).ToList <Process>();

            RefreshProcessesList(filteredprocesses, toolStripTextBox1.Text);
        }

        private void listView1_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            comparer.ColumnIndex = e.Column;

            comparer.SortDirection = comparer.SortDirection == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;

            listView1.ListViewItemSorter = comparer;

            listView1.Sort();
        }
        
        private void выходToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
    }
}
