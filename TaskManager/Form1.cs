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

namespace TaskManager
{
    public partial class Form1 : Form
    {
        //Загружаем обновленный список процессов. Снимок процессов на текущий момент после обновления.
        private List<Process> processes = null;
        //Экземпляр списка для сортировки.
        private ListViewItemComparer comparer = null;

        public Form1()
        {
            InitializeComponent();
        }
        //Заполнение списка(работает только с бэкендом,не взаим. с интерфейсом)
        private void GetProcesses() 
        {
            processes.Clear();
            processes = Process.GetProcesses().ToList<Process>();
        }
        //Уже взаим. с интерфейсом
        private void RefreshProcessesList() 
        {
            double memSize = 0;

            listView1.Items.Clear();

            foreach (Process p in processes) 
            {
                memSize = 0;     
                PerformanceCounter pc = new PerformanceCounter();
                
                pc.CategoryName = "Process";
                pc.CounterName = "Working Set - Private";
                pc.InstanceName = p.ProcessName;

                memSize = (double)pc.NextValue() / (1000 * 1000);
                //Массив строк в колонки
                string[] row = new string[] { p.ProcessName.ToString(), Math.Round(memSize, 1).ToString(), p.Id.ToString(), p.BasePriority.ToString(),p.ProcessName.ToString()};

                listView1.Items.Add(new ListViewItem(row));

                pc.Close();
                pc.Dispose();
            }
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
            processes = new List<Process>();

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
                    Process processToKill = processes.Where((x) => x.ProcessName ==
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
                    Process processToKill = processes.Where((x) => x.ProcessName ==
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
                    Process processToKill = processes.Where((x) => x.ProcessName ==
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

            List<Process> filteredprocesses = processes.Where((x) => 
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
