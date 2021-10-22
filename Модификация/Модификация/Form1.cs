using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Npgsql;

namespace Модификация
{
    public partial class Form1 : Form
    {

        private string connStr = new NpgsqlConnectionStringBuilder // поле для создания подключения к БД
        {
            Host = "localhost",
            Port = 5432,
            Database = "laba_2",
            Username = "postgres",
            Password = "postgres"
        }.ConnectionString;

        public Form1()
        {
            InitializeComponent();
            btnEdit.Enabled = false; // ограничения на доступ к кнопкам работы с данными в начале работы приложения
            btnDelete.Enabled = false;
            btnAdd.Enabled = false;

            ShowUsersList(); // вывод данных из БД
        }

        private void ShowUsersList() // метод для вывода данных из БД
        {
            lvUsers.Items.Clear();

            using (var conn = new NpgsqlConnection(connStr))
            {
                conn.Open();

                using (var sqlCommand = new NpgsqlCommand
                {
                    Connection = conn,
                    CommandText = @"SELECT id, login, time FROM users" // запрос на получение данных из БД
                })
                {
                    var reader = sqlCommand.ExecuteReader();
                    while (reader.Read())
                    {
                        var id = (int)reader["id"]; // читаем id
                        var login = (string)reader["login"]; // читаем логин
                        var registration_date = ((DateTime)reader["time"]).ToString("d"); // читаем время регистрации

                        var lvuser = new ListViewItem($"{login} ({registration_date})") // в поле tag обьекта ListViev для пользователя кладём id
                        {
                            Tag = id
                        };
                        lvUsers.Items.Add(lvuser); // добавляем пользователя
                    }

                    conn.Close();

                }
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        { 

        }

        private void flowLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void btnRefresh_Click(object sender, EventArgs e) // обработка кнопки обновления списка данных в форме
        {
            ShowUsersList();
            btnEdit.Enabled = false; // блокируем кнопки тк выделение слетает при обновлении формы 
            btnDelete.Enabled = false;
        }

        private void btnEdit_Click(object sender, EventArgs e) // изменени полей о пользователе
        {

            var login = tbLogin.Text;
            var reg_date = dtpRegDate.Value;
            var password = tbPassword.Text;
            var password_repeat = tbRepeatedPassword.Text;
            var base_pas = "";
            var user_id = (int)lvUsers.SelectedItems[0].Tag;

            using (var conn = new NpgsqlConnection(connStr))
            {
                conn.Open();

                using (var sqlCommand = new NpgsqlCommand // формируем завпрос получения пароля пользователя
                {
                    Connection = conn,
                    CommandText = @"SELECT password
                                        FROM users
                                           WHERE id=@id"
                })
                {
                    sqlCommand.Parameters.AddWithValue("id", user_id); // используем параметр дабы защитится от sql иньекций

                    var reader = sqlCommand.ExecuteReader();

                    if (reader.Read())
                    {
                        base_pas = (string)reader["password"];
                    }
                    else
                    {
                        base_pas = "";
                        state_label.Text = "Ошибка, пользователь не найден";
                        conn.Close();
                        return;
                    }
                    conn.Close();
                }
            }

            if (!BCrypt.Net.BCrypt.Verify(password, base_pas) && password!="") // проверяем отличается ли введеный пароль от хранящегося в бд
            {
                base_pas = BCrypt.Net.BCrypt.HashPassword(password); // если отличается то получаем новую хэш строку для него
            }


            using (var conn = new NpgsqlConnection(connStr)) // формируем новый запрос на изменение данных( сновым подключением)
            {
                conn.Open();

                using (var sqlCommand = new NpgsqlCommand
                {
                    Connection = conn,
                    CommandText = @"UPDATE users
                                        SET login = @login, password = @password, time = @reg_date
                                        WHERE id= @id"
                })
                {

                    sqlCommand.Parameters.AddWithValue("id", user_id);// используем параметр дабы защитится от sql иньекций
                    sqlCommand.Parameters.AddWithValue("login", login);
                    sqlCommand.Parameters.AddWithValue("reg_date", reg_date);
                    sqlCommand.Parameters.AddWithValue("password", base_pas);

                    if (sqlCommand.ExecuteNonQuery() == 0)
                    {
                        state_label.Text = "Ошибка, пользователь не найден";
                        conn.Close();
                    }
                    conn.Close();
                }
            }
        }

        private void lvUsers_SelectedIndexChanged(object sender, EventArgs e) // проверка выделеных пользователей в форме
        {
            var state_select_items = lvUsers.SelectedItems.Count > 0; // проверка выделили ли мы хоть кого нибудь
            btnDelete.Enabled = state_select_items;
            if (state_select_items) // если да
            {
                int user_id = (int)lvUsers.SelectedItems[0].Tag; // получаем id из тэга

                using (var conn = new NpgsqlConnection(connStr))
                {
                    conn.Open();

                    using (var sqlCommand = new NpgsqlCommand // формируем запрос на получение логина и времени
                    {
                        Connection = conn,
                        CommandText = @"SELECT login, time
                                        FROM users
                                           WHERE id=@id"
                    })
                    {
                        sqlCommand.Parameters.AddWithValue("id", user_id);// используем параметр дабы защитится от sql иньекций

                        var reader = sqlCommand.ExecuteReader();

                        if (reader.Read()) // мы их читали что бы потом сунуть в текст-бокс что бы пользователь мог понять с какими именно данными имеет дело
                        {
                            tbLogin.Text = (string)reader["login"];
                            dtpRegDate.Value = (DateTime)reader["time"];
                        }
                        conn.Close();
                    }
                }
            }
        }

        private void btnAdd_Click(object sender, EventArgs e) // добавление пользователя
        {
            using (var conn = new NpgsqlConnection(connStr))
            {
                conn.Open();

                var login = tbLogin.Text;

                using (var sqlCommand = new NpgsqlCommand // создаём запрос на проверку введенного логина, то есть есть ли такой логин в базе
                {
                    Connection = conn,
                    CommandText = @"SELECT COUNT(*)
                                    FROM users
                                    WHERE login = @userLogin"
                })
                {
                    sqlCommand.Parameters.AddWithValue("@userLogin", login);// используем параметр дабы защитится от sql иньекций

                    if ((long)sqlCommand.ExecuteScalar() > 0)
                    {
                        state_label.Text = "Логин уже занят";
                        return;
                    }

                    state_label.Text = "";
                }

                var password = tbPassword.Text;

                var passwordHash = BCrypt.Net.BCrypt.HashPassword(password); // хэшируем пароль

                using (var sqlCommand = new NpgsqlCommand // запрос на вставку пользователя в БД
                {
                    Connection = conn,
                    CommandText = @"INSERT INTO users (login, password)
                                    VALUES (@login, @passwordHash)"
                })
                {
                    sqlCommand.Parameters.AddWithValue("@login", login);// используем параметр дабы защитится от sql иньекций
                    sqlCommand.Parameters.AddWithValue("@passwordHash", passwordHash);


                    if (sqlCommand.ExecuteNonQuery() == 1)
                    {
                        state_label.Text = "Пользователь успешно зарегистрирован";
                    }
                    else
                    {
                        state_label.Text = "Ошибка регистрации. Попробуйте позже";
                    }
                }
                conn.Close();
            }
        }

        private void Form1_MouseClick(object sender, MouseEventArgs e)
        {
            
        }

        private void Form1_KeyPress(object sender, KeyPressEventArgs e)
        {
            
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            
        }

        private void Form1_KeyUp(object sender, KeyEventArgs e)
        {
            
        }

        private void Form1_MouseClick_1(object sender, MouseEventArgs e)
        {
        }

        private void flowLayoutPanel1_MouseClick(object sender, MouseEventArgs e) // защита от невалидных значений
        {
            if(tbLogin.Text == "") // проверка на то что все поля не пустые
            {
                state_label.Text = "Заполните все поля";
                btnAdd.Enabled = false;
                btnEdit.Enabled = false;
                return;
            }

            
            if(!string.Equals(tbPassword.Text,tbRepeatedPassword.Text)) //пароли совпадают
            {
                state_label.Text = "Пароль не совпадает с повторением";
                btnAdd.Enabled = false;
                btnEdit.Enabled = false;
                return;
            }

            //проверка валидностьи пароля

            var password1 = tbPassword.Text;

            var legal_elem1 = "qwertyuiopasdfghjklzxcvbnm";
            var legal_elem2 = "QWERTYUIOPASDFGHJKLZXCVBNM";
            var legal_elem3 = "1234567890";
            var legal_elem4 = "/|!@#$%^&*(){}[]<>.,`~;:'+-_";

            var a = legal_elem1.Except(password1);
            var b = legal_elem2.Except(password1);
            var c = legal_elem3.Except(password1);
            var d = legal_elem4.Except(password1);


            if (!(a.Count() < legal_elem1.Count() && b.Count() < legal_elem2.Count() && c.Count() < legal_elem3.Count()
                && d.Count() < legal_elem4.Count()) && tbPassword.Text!="")
            {
                state_label.Text = "В пароле должны присутствовать прописные и заглавные латинские буквы цифры и знаки  /|!@#$%^&*(){}[]<>.,`~;:'+-_";
                return;
            }

            using (var conn = new NpgsqlConnection(connStr)) // создание запроса на проверку был ли такой логин
                {
                    conn.Open();

                    using (var sqlCommand = new NpgsqlCommand
                    {
                        Connection = conn,
                        CommandText = @"SELECT COUNT(*)
                                        FROM users
                                           WHERE login=@login"
                    })
                    {
                        sqlCommand.Parameters.AddWithValue("login", tbLogin.Text);// используем параметр дабы защитится от sql иньекций

                    if ((long)sqlCommand.ExecuteScalar() > 0)
                        {
                            state_label.Text = "Логин занят";
                            btnAdd.Enabled = false;
                            btnEdit.Enabled = false;
                            return;
                        }



                    state_label.Text = "";
                        conn.Close();
                    }
                }

            if (tbPassword.Text == "" || tbRepeatedPassword.Text == "")
            {
                state_label.Text = "Заполните все поля";
                btnAdd.Enabled = false;
            }
            else
            {
                btnAdd.Enabled = true;
            }


            if (lvUsers.SelectedItems.Count > 0) // проверка на наличие выделенных полей
            {
                btnEdit.Enabled = true;
            }
            else
            {
                return;
            }
                state_label.Text = "";
        }

        private void btnDelete_Click(object sender, EventArgs e) //удаление
        {
            using (var conn = new NpgsqlConnection(connStr))
            {
                conn.Open();

                for (int i = 0; i < lvUsers.SelectedItems.Count; i++) //последовательно кидаем запросы на удаление всех выделенные записи
                {

                    using (var sqlCommand = new NpgsqlCommand
                    {
                        Connection = conn,
                        CommandText = @"DELETE
                                        FROM users
                                           WHERE id=@id"
                    })
                    {
                        sqlCommand.Parameters.AddWithValue("id", lvUsers.SelectedItems[i].Tag);// используем параметр дабы защитится от sql иньекций

                        if (sqlCommand.ExecuteNonQuery() != 0)
                        {
                            state_label.Text = "Пользователь успешно удалён";
                        }
                        else
                        {
                            state_label.Text = "Произошла ошибка";
                        }
                    }
                }
                conn.Close();
            }
        }
    }
}
