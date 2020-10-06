using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using mediNetConsult_Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Net;
using System.Net.Http;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Net.Mail;
//using System.Web.Http;



namespace mediNetConsult_Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MediNetConsultController : ControllerBase
    {
        private IConfiguration _config;
        public MediNetConsultController(IConfiguration config)
        {
            this._config = config;
        }

        #region login
        // GET: api/MediNetConsult
        [HttpGet]
        public IActionResult Login(string Email, string password)
        {
            SignIn signin = new SignIn();
            signin.Email = Email;
            signin.Password = password;
            IActionResult response = Unauthorized();

            var user = AuthenticateUser(signin);
            if (user != null)
            {
                var tokenStr = GenerateJSONWebTokenForLogin(user);
                response = Ok(new { token = tokenStr });
            }
            if (response != null && user!=null)
            {
                response = Ok(new { Status = "Success", UserProfile = "Welcome " + user.FirstName, Id = user.id });               
            }
            else
            {
                response = Ok(new { Status = "UnSuccessFull", Token = "Not Available" });
            }
            return response;
        }

        private SignIn AuthenticateUser(SignIn signin)
        {
            SignIn sign = null;
            var data = loginDetails(signin, _config);
            if (data != null && data.Rows.Count!=0)
            {
                // return data;
                sign = new SignIn
                {
                    id = data.Rows[0]["id"].ToString(),
                    FirstName = data.Rows[0]["firstname"].ToString(),
                    LastName = data.Rows[0]["lastname"].ToString(),
                    Email = data.Rows[0]["email"].ToString(),
                    Password = data.Rows[0]["password"].ToString(),                  
                    Mobile = data.Rows[0]["mobile"].ToString(),

                };
            }

            return sign;


        }

        private DataTable loginDetails(SignIn signin, IConfiguration config)
        {
            var conString = config.GetSection("Data").GetSection("ConnectionStrings").Value;
            DataTable dt = new DataTable();
            NpgsqlConnection con = new NpgsqlConnection(conString);
            try
            {
                con.Open();
                NpgsqlCommand cmd = new NpgsqlCommand();
                cmd.Connection = con;
                cmd.CommandText = "SELECT id, firstname, lastname, email, mobile, providernumber, isactive, password FROM registration where email='"+signin.Email+"' and password='"+signin.Password +"'"; 
                cmd.CommandType = CommandType.Text;
                NpgsqlDataAdapter da = new NpgsqlDataAdapter(cmd);
               
                da.Fill(dt);              
                cmd.Dispose();
                con.Close();
                return dt;

            }
            catch (Exception ex)
            {
                if (con.State == ConnectionState.Open)
                {
                    con.Close();
                }
                return dt;
            }
        }

        private string GenerateJSONWebTokenForLogin(SignIn singupinfo)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.Default.GetBytes(_config["Jwt:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub,singupinfo.FirstName),
                new Claim(JwtRegisteredClaimNames.Email,singupinfo.Email),
                 new Claim(JwtRegisteredClaimNames.Jti,Guid.NewGuid().ToString()),
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Issuser"],
                claims,
                expires: DateTime.Now.AddMinutes(120),

                signingCredentials: credentials);

            var encodetoken = new JwtSecurityTokenHandler().WriteToken(token);
            return encodetoken;

        }

        #endregion

        #region forgotpassword
        [HttpGet("ForGotPassword")]

        public IActionResult ForGotPassword(string Param)
        {
            IActionResult response = Unauthorized();
            var tokenStr = "";
            ForgotPassword Fgpass = new ForgotPassword();
            Fgpass.Param = Param;
            Fgpass.Password = "Sandhya123";
            var user = AuthenticatePassword(Fgpass);
            if (user != null)
            {
                tokenStr = GenerateJSONWebTokenForForgot(user);
                response = Ok(new { token = tokenStr });
            }
            if (response != null)
            {
                response = Ok(new { status="Success", UserProfile = "Welcome " + user.Email, token = tokenStr, Id = user.Id, New_Password = user.Password, Messages = "Password Updated Successfully." });
            }
            else
            {
                response = Ok(new { Token = "Not Available" });
            }
            return response;
        }

        private ForgotPassword AuthenticatePassword(ForgotPassword Fgpass)
        {
            ForgotPassword FgPass = null;
            var data = ForgotPasswordDetails(Fgpass, _config);
            if (data != null)
            {
                // return data;
                FgPass = new ForgotPassword
                {
                    Id = Convert.ToInt32(data.Rows[0]["id"]),
                    Email = data.Rows[0]["email"].ToString(),
                    Password = data.Rows[0]["password"].ToString(),


                };
            }

            return FgPass;
        }

        private DataTable ForgotPasswordDetails(ForgotPassword fgPass, IConfiguration config)
        {
            var conString = config.GetSection("Data").GetSection("ConnectionStrings").Value;
            DataTable dt = new DataTable();
            NpgsqlConnection con = new NpgsqlConnection(conString);
            try
            {
               // var query = "update registration set password='" + fgPass.Password + "' where id=" + fgPass.Id + "returning email";
               // NpgsqlCommand cmd = new NpgsqlCommand(query, con);
              //  con.Open();
               // cmd.ExecuteNonQuery();
               // con.Close();
                con.Open();
                NpgsqlCommand cmd = new NpgsqlCommand();
                cmd.Connection = con;
                cmd.CommandText = "update registration set password='" + fgPass.Password + "' where email='" + fgPass.Param + "' returning id,email,password";
                cmd.CommandType = CommandType.Text;
                NpgsqlDataAdapter da = new NpgsqlDataAdapter(cmd);               
                da.Fill(dt);
                //Sending activation link in the email
                //  string To = "purvesh@rlogical.com";
                string To = dt.Rows[0]["email"].ToString();
                string Subject = "Regarding Password Reset";
                string Body = "Your Password Is Reset Successfully";
                Body += " Your New Password is :'" + dt.Rows[0]["Password"].ToString() + "'";
                MailMessage mm = new MailMessage();
                mm.To.Add(To);
                mm.Subject = Subject;
                mm.Body = Body;
                mm.From = new MailAddress("Sandhya@rlogical.com");
                mm.IsBodyHtml = false;
                SmtpClient smtp = new SmtpClient("smtp.gmail.com");
                // smtp.Host= "smtp.o.com";
                smtp.Port = 587;

                smtp.UseDefaultCredentials = true;
                smtp.EnableSsl = true;
                smtp.Credentials = new System.Net.NetworkCredential("Sandhya@rlogical.com", "Rlogical@123");
                smtp.Send(mm);                
                return dt;
            }
            catch (Exception ex)
            {
                if (con.State == ConnectionState.Open)
                {
                    con.Close();
                }
                return dt;
            }
        }

        private string GenerateJSONWebTokenForForgot(ForgotPassword Finfo)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.Default.GetBytes(_config["Jwt:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub,Finfo.Password),
                new Claim(JwtRegisteredClaimNames.Email,Finfo.Email),
                 new Claim(JwtRegisteredClaimNames.Jti,Guid.NewGuid().ToString()),
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Issuser"],
                claims,
                expires: DateTime.Now.AddMinutes(120),

                signingCredentials: credentials);

            var encodetoken = new JwtSecurityTokenHandler().WriteToken(token);
            return encodetoken;

        }
        #endregion

        #region homeContactList

        // GET: api/MediNetConsult/5
        [HttpGet("{id}", Name = "ContactList")]
        public IActionResult ContactList(int id)
        {
            IActionResult response = Unauthorized();
            var user = getContactList(id, _config);
           // List<Contact> data = new List<Contact>();

          var  data = (from DataRow row in user.Rows
                    select new Contact
                    {
                        id = row["id"] == DBNull.Value ? "" : Convert.ToString(row["id"]),
                        Date = row["date"] == DBNull.Value ? "" : Convert.ToString(row["date"]),
                        Mobile = row["mobile"] == DBNull.Value ? "" : Convert.ToString(row["mobile"]),

                    }).ToList();
            if (user != null)
            {
              
                response = Ok(new { Status = "Success", response = data});
            }
            return response;
        }

        private DataTable getContactList(int id, IConfiguration config)
        {
            var conString = config.GetSection("Data").GetSection("ConnectionStrings").Value;
            DataTable dt = new DataTable();
            NpgsqlConnection con = new NpgsqlConnection(conString);
            try
            {
                con.Open();
                NpgsqlCommand cmd = new NpgsqlCommand();
                cmd.Connection = con;
                cmd.CommandText = "SELECT id, date, mobile FROM contact where regid='" + id + "' and date >'"+DateTime.Now.AddDays(-7)+"'order by id desc";
                cmd.CommandType = CommandType.Text;
                NpgsqlDataAdapter da = new NpgsqlDataAdapter(cmd);

                da.Fill(dt);
                cmd.Dispose();
                con.Close();
                return dt;

            }
            catch (Exception ex)
            {
                if (con.State == ConnectionState.Open)
                {
                    con.Close();
                }
                return dt;
            }
        }

        [HttpPost("ConsultList")]
        public IActionResult ConsultList(string regid,string mobile)
        {
            Contact obj = new Contact();
            obj.DateTime = DateTime.Now;
            obj.Mobile = mobile;
            obj.regid = regid;
           string result= AddContact(obj, _config);
            return Ok(new { Status = result, id = regid, mobile=mobile });
        }

        private string AddContact(Contact obj, IConfiguration config)
        {
            var conString = config.GetSection("Data").GetSection("ConnectionStrings").Value;
            NpgsqlConnection con = new NpgsqlConnection(conString);
            try
            {
                con.Open();
                NpgsqlCommand cmd = new NpgsqlCommand();
                cmd.Connection = con;
                cmd.CommandText = "insert into contact(date, mobile,regid) values('" + obj.DateTime + "','" + obj.Mobile + "','" + obj.regid + "')";
                cmd.CommandType = CommandType.Text;
                NpgsqlDataAdapter da = new NpgsqlDataAdapter(cmd);
                DataTable dt = new DataTable();
                da.Fill(dt);              
                cmd.Dispose();
                con.Close();
                return "Success";
            }
            catch (Exception ex)
            {
                return "insertion unsuccessfull!!";
            }
        }

        #endregion
        #region signUp
        // POST: api/MediNetConsult
        [HttpPost("SignUp")]
        public IActionResult SignUp(string FirstName, string LastName, string Email, string MobileNumber, string ProviderNumber, string IsActive)
        {
           
            SignUp signup = new SignUp();
            signup.FirstName = FirstName;
            signup.LastName = LastName;
            signup.Email = Email;
            signup.Mobile = MobileNumber;
            signup.ProviderNumber = ProviderNumber;
            signup.IsActive = IsActive;
            string results = AddRecord(signup, _config);
            return Ok(new { Status = "Success", id= results });
           // return results;
        }

        private string AddRecord(SignUp obj, IConfiguration config)
        {
            var conString = config.GetSection("Data").GetSection("ConnectionStrings").Value;
            NpgsqlConnection con = new NpgsqlConnection(conString);
            try
            {
                con.Open();
                NpgsqlCommand cmd = new NpgsqlCommand();
                cmd.Connection = con;
                cmd.CommandText = "insert into registration(firstname, lastname, email, mobile, providernumber, isactive) values('" + obj.FirstName + "','" + obj.LastName + "','" + obj.Email + "','" + obj.Mobile + "','" + obj.ProviderNumber + "','" + obj.IsActive + "') returning id";
                cmd.CommandType = CommandType.Text;
                NpgsqlDataAdapter da = new NpgsqlDataAdapter(cmd);
                DataTable dt = new DataTable();
                da.Fill(dt);
                string id = dt.Rows[0]["id"].ToString();
                cmd.Dispose();
                con.Close();
                //  var query = "insert into registration(firstname, lastname, email, mobile, providernumber, isactive) values('" + obj.FirstName + "','" + obj.LastName + "','"+ obj.Email + "','"+ obj.Mobile + "','"+ obj.ProviderNumber + "','" +obj.IsActive+ "') returning id";
                //  NpgsqlCommand cmd = new NpgsqlCommand(query,con);
                //  con.Open();              
                //  int id=cmd.ExecuteNonQuery();
                // int id = (int)cmd.Parameters["id"].Value;
                //con.Close();
                // return id.ToString();
                return id;
            }
            catch(Exception ex)
            {
                return "insertion unsuccessfull!!";
            }
           
        }

        // PUT: api/MediNetConsult/5
        [HttpPut("{id}")]
        public IActionResult InsertPassword(int id,string password)
        {
            var tokenStr = "";
            SignUp signup = new SignUp();
            signup.id = id.ToString();
            signup.Password = password;         
            string results = AddPassword(signup, _config);
            if (results != null || results != "")
            {
                tokenStr = GenerateJSONWebToken(signup);
                return Ok(new { Status = "Success", token = tokenStr, Message = "'" + results + "'" });
            }
            else
            {
                return Ok(new { Status = "Fail", Message = "'" + results + "'" });
            }
        }

        private string AddPassword(SignUp signup, IConfiguration config)
        {
            var conString = config.GetSection("Data").GetSection("ConnectionStrings").Value;
            NpgsqlConnection con = new NpgsqlConnection(conString);
            try
            {
                var query = "update registration set password='"+signup.Password+"' where id="+ signup.id +"";
                NpgsqlCommand cmd = new NpgsqlCommand(query, con);
                con.Open();
                cmd.ExecuteNonQuery();
                con.Close();
                return "Updated Successfully!!";
            }
            catch (Exception ex)
            {
                return "Updatation unsuccessfull!!";
            }
        }

        private string GenerateJSONWebToken(SignUp singupinfo)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.Default.GetBytes(_config["Jwt:Key"]));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub,singupinfo.id),
                new Claim(JwtRegisteredClaimNames.Email,singupinfo.Password),
                 new Claim(JwtRegisteredClaimNames.Jti,Guid.NewGuid().ToString()),
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Issuser"],
                claims,
                expires: DateTime.Now.AddMinutes(120),

                signingCredentials: credentials);

            var encodetoken = new JwtSecurityTokenHandler().WriteToken(token);
            return encodetoken;

        }

        #endregion

        // DELETE: api/ApiWithActions/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
