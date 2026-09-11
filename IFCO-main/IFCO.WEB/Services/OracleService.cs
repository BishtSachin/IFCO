using IFCO.WEB.Helpers;
using IFCO.WEB.Models;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;
using ClosedXML.Excel;
namespace IFCO.WEB.Services
{
    public class OracleService
    {
        private readonly string _connectionString;
        private readonly ILogger<OracleService> _logger;
        public OracleService(IConfiguration configuration, ILogger<OracleService> logger)
        {
            _logger = logger;

            // 1. Read the ENCRYPTED string from configuration
            var encryptedConnectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            // 2. Decrypt the string using your custom helper
            try
            {
                _connectionString = EncryptionHelper.Decrypt(encryptedConnectionString);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to decrypt the database connection string. The application cannot start.");
                throw;
            }
        }
        #region Vertical Master
        public async Task<(List<VerticalMaster> Verticals, int TotalRecords)> SearchVerticalsAsync(string? searchTerm = null, string? status = null, int pageNumber = 1, int pageSize = 10)
        {
            var verticals = new List<VerticalMaster>();
            int totalRecords = 0;
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.VERTICAL_MASTER_PKG.search_verticals", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        // These parameters now correctly match the database procedure
                        command.Parameters.Add("p_SEARCH_TERM", OracleDbType.Varchar2, searchTerm, ParameterDirection.Input);
                        command.Parameters.Add("p_STATUS", OracleDbType.NVarchar2, status, ParameterDirection.Input);
                        command.Parameters.Add("p_PAGE_NUMBER", OracleDbType.Int32, pageNumber, ParameterDirection.Input);
                        command.Parameters.Add("p_PAGE_SIZE", OracleDbType.Int32, pageSize, ParameterDirection.Input);
                        command.Parameters.Add("p_TOTAL_RECORDS", OracleDbType.Int32, ParameterDirection.Output);
                        command.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                        await command.ExecuteNonQueryAsync();

                        if (command.Parameters["p_TOTAL_RECORDS"].Value is OracleDecimal oracleDecimal && !oracleDecimal.IsNull)
                        {
                            totalRecords = oracleDecimal.ToInt32();
                        }

                        using (var reader = ((OracleRefCursor)command.Parameters["p_RESULT_CURSOR"].Value).GetDataReader())
                        {
                            while (await reader.ReadAsync())
                            {
                                verticals.Add(new VerticalMaster
                                {
                                    SqNo = reader.GetInt32(reader.GetOrdinal("SQ_NO")),
                                    VerticalId = reader.GetString(reader.GetOrdinal("VERTICAL_ID")),
                                    VerticalName = reader.GetString(reader.GetOrdinal("VERTICAL_NAME")),
                                    Status = reader.GetString(reader.GetOrdinal("STATUS"))
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SearchVerticalsAsync");
                throw;
            }
            return (verticals, totalRecords);
        }

        public async Task AddVerticalAsync(VerticalMaster vertical, string createdBy)
        {
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.VERTICAL_MASTER_PKG.add_vertical", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("p_VERTICAL_ID", OracleDbType.NVarchar2, vertical.VerticalId, ParameterDirection.Input);
                        command.Parameters.Add("p_VERTICAL_NAME", OracleDbType.NVarchar2, vertical.VerticalName, ParameterDirection.Input);
                        command.Parameters.Add("p_STATUS", OracleDbType.NVarchar2, vertical.Status, ParameterDirection.Input);
                        command.Parameters.Add("p_CREATED_BY", OracleDbType.NVarchar2, createdBy, ParameterDirection.Input);
                        command.Parameters.Add("p_SQ_NO", OracleDbType.Int32, ParameterDirection.Output);
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calling AddVerticalAsync method");
                throw;
            }
        }
        public async Task<VerticalMaster> GetVerticalByIdAsync(int sqNo)
        {
            try
            {
                VerticalMaster? vertical = null;
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.VERTICAL_MASTER_PKG.get_vertical_by_id", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("p_SQ_NO", OracleDbType.Int32, sqNo, ParameterDirection.Input);
                        command.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);
                        await command.ExecuteNonQueryAsync();
                        using (var reader = ((OracleRefCursor)command.Parameters["p_RESULT_CURSOR"].Value).GetDataReader())
                        {
                            if (await reader.ReadAsync())
                            {
                                vertical = new VerticalMaster
                                {
                                    SqNo = reader.GetInt32(reader.GetOrdinal("SQ_NO")),
                                    VerticalId = reader.GetString(reader.GetOrdinal("VERTICAL_ID")), // <-- ADD THIS LINE
                                    VerticalName = reader.GetString(reader.GetOrdinal("VERTICAL_NAME")),
                                    Status = reader.GetString(reader.GetOrdinal("STATUS"))
                                };
                            }
                        }
                    }
                }
                return vertical ?? throw new Exception($"Vertical with SQ_NO {sqNo} not found.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calling method GetVerticalByIdAsync");
                throw;
            }
        }

        public async Task UpdateVerticalAsync(VerticalMaster vertical, string updatedBy)
        {
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.VERTICAL_MASTER_PKG.update_vertical", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("p_SQ_NO", OracleDbType.Int32, vertical.SqNo, ParameterDirection.Input);
                        command.Parameters.Add("p_VERTICAL_NAME", OracleDbType.NVarchar2, vertical.VerticalName, ParameterDirection.Input);
                        command.Parameters.Add("p_STATUS", OracleDbType.NVarchar2, vertical.Status, ParameterDirection.Input);
                        command.Parameters.Add("p_UPDATED_BY", OracleDbType.NVarchar2, updatedBy, ParameterDirection.Input);
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calling method UpdateVerticalAsync");
                throw;
            }
        }

        public async Task SetVerticalStatusAsync(int sqNo, string newStatus, string updatedBy)
        {
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.VERTICAL_MASTER_PKG.set_vertical_status", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("p_SQ_NO", OracleDbType.Int32, sqNo, ParameterDirection.Input);
                        command.Parameters.Add("p_STATUS", OracleDbType.NVarchar2, newStatus, ParameterDirection.Input);
                        command.Parameters.Add("p_UPDATED_BY", OracleDbType.NVarchar2, updatedBy, ParameterDirection.Input);
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calling method SetVerticalStatusAsync");
                throw;
            }
        }
        public async Task<List<RcmMaster>> GetRcmsByVerticalAndRoleAsync(string? verticalId, string role)
        {
            var rcms = new List<RcmMaster>();
            using (var conn = new OracleConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new OracleCommand("IFCO.VERTICAL_MASTER_PKG.get_rcms_by_vertical_and_role", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_VERTICAL_ID", OracleDbType.NVarchar2, verticalId, ParameterDirection.Input);
                    cmd.Parameters.Add("p_ROLE", OracleDbType.Varchar2, role, ParameterDirection.Input);
                    cmd.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            rcms.Add(new RcmMaster
                            {
                                SqNo = Convert.ToInt32(reader["SQ_NO"]),
                                RcmId = reader["RCM_ID"].ToString(),
                                RcmName = reader["RCM_NAME"].ToString(),
                                Status = reader["STATUS"].ToString(),
                            });
                        }
                    }
                }
            }
            return rcms;
        }
        #endregion
        #region Consultant Master
        public async Task<(List<ConsultantMaster> Consultants, int TotalRecords)> SearchConsultantsAsync(string? searchTerm = null, string? consultantType = null, int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                var consultants = new List<ConsultantMaster>();
                int totalRecords = 0;

                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.CONSULTANT_MASTER_PKG.search_consultants", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        // Input Parameters
                        command.Parameters.Add("p_SEARCH_TERM", OracleDbType.Varchar2, searchTerm, ParameterDirection.Input);
                        command.Parameters.Add("p_CONSULTANT_TYPE", OracleDbType.NVarchar2, consultantType, ParameterDirection.Input);
                        command.Parameters.Add("p_STATUS", OracleDbType.NVarchar2, null, ParameterDirection.Input);
                        command.Parameters.Add("p_PAGE_NUMBER", OracleDbType.Int32, pageNumber, ParameterDirection.Input);
                        command.Parameters.Add("p_PAGE_SIZE", OracleDbType.Int32, pageSize, ParameterDirection.Input);
                        // Output Parameters
                        command.Parameters.Add("p_TOTAL_RECORDS", OracleDbType.Int32, ParameterDirection.Output);
                        command.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                        await command.ExecuteNonQueryAsync();

                        totalRecords = Convert.ToInt32(command.Parameters["p_TOTAL_RECORDS"].Value.ToString());

                        using (var reader = ((OracleRefCursor)command.Parameters["p_RESULT_CURSOR"].Value).GetDataReader())
                        {
                            while (await reader.ReadAsync())
                            {
                                consultants.Add(new ConsultantMaster
                                {
                                    SqNo = reader.GetInt32(reader.GetOrdinal("SQ_NO")),
                                    ConsultantId = reader.GetString(reader.GetOrdinal("CONSULTANT_ID")),
                                    ConsultantName = reader.GetString(reader.GetOrdinal("CONSULTANT_NAME")),
                                    ConsultantType = reader.GetString(reader.GetOrdinal("CONSULTANT_TYPE")),
                                    MobileNumber = reader.GetString(reader.GetOrdinal("MOBILE_NUMBER")),
                                    EmailId = reader.IsDBNull(reader.GetOrdinal("EMAIL_ID")) ? null : reader.GetString(reader.GetOrdinal("EMAIL_ID")),
                                    Status = reader.GetString(reader.GetOrdinal("STATUS")),
                                    CheckerSqNo = reader.IsDBNull(reader.GetOrdinal("CHECKER_SQ_NO")) ? null : reader.GetInt32(reader.GetOrdinal("CHECKER_SQ_NO")),
                                    CheckerName = reader.IsDBNull(reader.GetOrdinal("CHECKER_NAME")) ? null : reader.GetString(reader.GetOrdinal("CHECKER_NAME")),
                                    CreatedBy = reader.GetString(reader.GetOrdinal("CREATED_BY")),
                                    CreatedDate = reader.GetDateTime(reader.GetOrdinal("CREATED_DATE")),
                                    UpdatedBy = reader.IsDBNull(reader.GetOrdinal("UPDATED_BY")) ? null : reader.GetString(reader.GetOrdinal("UPDATED_BY")),
                                    UpdatedDate = reader.IsDBNull(reader.GetOrdinal("UPDATED_DATE")) ? null : reader.GetDateTime(reader.GetOrdinal("UPDATED_DATE"))
                                });
                            }
                        }
                    }
                }
                return (consultants, totalRecords);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calling SearchConsultantsAsync method");
                throw;
            }
        }

        public async Task AddConsultantAsync(ConsultantMaster consultant, string createdBy)
        {
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.CONSULTANT_MASTER_PKG.add_consultant", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        // Input Parameters
                        command.Parameters.Add("p_CONSULTANT_ID", OracleDbType.NVarchar2, consultant.ConsultantId, ParameterDirection.Input);
                        command.Parameters.Add("p_CONSULTANT_NAME", OracleDbType.NVarchar2, consultant.ConsultantName, ParameterDirection.Input);
                        command.Parameters.Add("p_CONSULTANT_TYPE", OracleDbType.NVarchar2, consultant.ConsultantType, ParameterDirection.Input);
                        command.Parameters.Add("p_MOBILE_NUMBER", OracleDbType.NVarchar2, consultant.MobileNumber, ParameterDirection.Input);
                        command.Parameters.Add("p_EMAIL_ID", OracleDbType.NVarchar2, consultant.EmailId, ParameterDirection.Input);
                        command.Parameters.Add("p_CHECKER_SQ_NO", OracleDbType.Int32, consultant.CheckerSqNo, ParameterDirection.Input);
                        command.Parameters.Add("p_CREATED_BY", OracleDbType.NVarchar2, createdBy, ParameterDirection.Input);

                        // Output Parameter
                        command.Parameters.Add("p_SQ_NO", OracleDbType.Int32, ParameterDirection.Output);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calling AddConsultantAsync method");
                throw;
            }
        }
        public async Task<List<ConsultantMaster>> GetCheckersAsync()
        {
            try
            {
                var result = await SearchConsultantsAsync(consultantType: "Checker", pageSize: 1000);
                return result.Consultants.Where(c => c.Status == "Active").ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calling GetCheckersAsync method");
                throw;
            }
        }

        public async Task<ConsultantMaster> GetConsultantByIdAsync(int sqNo)
        {
            try
            {
                ConsultantMaster? consultant = null;
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.CONSULTANT_MASTER_PKG.get_consultant_by_id", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("p_SQ_NO", OracleDbType.Int32, sqNo, ParameterDirection.Input);
                        command.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                        await command.ExecuteNonQueryAsync();

                        using (var reader = ((OracleRefCursor)command.Parameters["p_RESULT_CURSOR"].Value).GetDataReader())
                        {
                            if (await reader.ReadAsync())
                            {
                                consultant = new ConsultantMaster
                                {
                                    SqNo = reader.GetInt32(reader.GetOrdinal("SQ_NO")),
                                    ConsultantId = reader.GetString(reader.GetOrdinal("CONSULTANT_ID")),
                                    ConsultantName = reader.GetString(reader.GetOrdinal("CONSULTANT_NAME")),
                                    ConsultantType = reader.GetString(reader.GetOrdinal("CONSULTANT_TYPE")),
                                    MobileNumber = reader.GetString(reader.GetOrdinal("MOBILE_NUMBER")),
                                    EmailId = reader.IsDBNull(reader.GetOrdinal("EMAIL_ID")) ? null : reader.GetString(reader.GetOrdinal("EMAIL_ID")),
                                    Status = reader.GetString(reader.GetOrdinal("STATUS")),
                                    CheckerSqNo = reader.IsDBNull(reader.GetOrdinal("CHECKER_SQ_NO")) ? null : reader.GetInt32(reader.GetOrdinal("CHECKER_SQ_NO"))
                                };
                            }
                        }
                    }
                }
                return consultant ?? throw new Exception($"Consultant with SQ_NO {sqNo} not found.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calling method GetConsultantByIdAsync");
                throw;
            }
        }

        public async Task UpdateConsultantAsync(ConsultantMaster consultant, string updatedBy)
        {
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.CONSULTANT_MASTER_PKG.update_consultant", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        command.Parameters.Add("p_SQ_NO", OracleDbType.Int32, consultant.SqNo, ParameterDirection.Input);
                        command.Parameters.Add("p_CONSULTANT_NAME", OracleDbType.NVarchar2, consultant.ConsultantName, ParameterDirection.Input);
                        command.Parameters.Add("p_CONSULTANT_TYPE", OracleDbType.NVarchar2, consultant.ConsultantType, ParameterDirection.Input);
                        command.Parameters.Add("p_MOBILE_NUMBER", OracleDbType.NVarchar2, consultant.MobileNumber, ParameterDirection.Input);
                        command.Parameters.Add("p_EMAIL_ID", OracleDbType.NVarchar2, consultant.EmailId, ParameterDirection.Input);
                        command.Parameters.Add("p_CHECKER_SQ_NO", OracleDbType.Int32, consultant.CheckerSqNo, ParameterDirection.Input);
                        command.Parameters.Add("p_UPDATED_BY", OracleDbType.NVarchar2, updatedBy, ParameterDirection.Input);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calling method UpdateConsultantAsync");
                throw;
            }
        }
        public async Task SetConsultantStatusAsync(int sqNo, string newStatus, string updatedBy)
        {
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.CONSULTANT_MASTER_PKG.set_consultant_status", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        command.Parameters.Add("p_SQ_NO", OracleDbType.Int32, sqNo, ParameterDirection.Input);
                        command.Parameters.Add("p_STATUS", OracleDbType.NVarchar2, newStatus, ParameterDirection.Input);
                        command.Parameters.Add("p_UPDATED_BY", OracleDbType.NVarchar2, updatedBy, ParameterDirection.Input);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling SetConsultantStatusAsync method for SQ_NO {SqNo}", sqNo);
                throw;
            }
        }
        // NEW METHOD: Fetches the RCMs mapped to a specific consultant.
        public async Task<List<RcmMaster>> GetRcmsForConsultantAsync(string consultantId)
        {
            var rcms = new List<RcmMaster>();
            try
            {
                // First, get the SQ_NO for the given consultant ID
                var consultant = await GetConsultantByLoginIdAsync(consultantId);
                if (consultant == null) return rcms; // Return empty list if consultant not found

                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.CONSULTANT_MASTER_PKG.get_rcms_for_consultant", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("p_CONSULTANT_SQ_NO", OracleDbType.Int32, consultant.SqNo, ParameterDirection.Input);
                        command.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                        await command.ExecuteNonQueryAsync();

                        using (var reader = ((OracleRefCursor)command.Parameters["p_RESULT_CURSOR"].Value).GetDataReader())
                        {
                            while (await reader.ReadAsync())
                            {
                                rcms.Add(new RcmMaster
                                {
                                    SqNo = reader.GetInt32(reader.GetOrdinal("SQ_NO")),
                                    RcmId = reader.GetString(reader.GetOrdinal("RCM_ID")),
                                    RcmName = reader.GetString(reader.GetOrdinal("RCM_NAME"))
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetRcmsForConsultantAsync for Consultant ID {consultantId}", consultantId);
                throw;
            }
            return rcms;
        }
        #endregion
        #region RCM Master Methods
        public async Task<(List<RcmMaster> Rcms, int TotalRecords)> SearchRcmsAsync(string? searchTerm = null, int pageNumber = 1, int pageSize = 10)
        {
            var rcms = new List<RcmMaster>();
            int totalRecords = 0;
            using (var conn = new OracleConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new OracleCommand("IFCO.RCM_MASTER_PKG.search_rcms", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_SEARCH_TERM", OracleDbType.Varchar2, searchTerm, ParameterDirection.Input);
                    cmd.Parameters.Add("p_STATUS", OracleDbType.NVarchar2, null, ParameterDirection.Input); // Status filter can be added later if needed
                    cmd.Parameters.Add("p_PAGE_NUMBER", OracleDbType.Int32, pageNumber, ParameterDirection.Input);
                    cmd.Parameters.Add("p_PAGE_SIZE", OracleDbType.Int32, pageSize, ParameterDirection.Input);
                    cmd.Parameters.Add("p_TOTAL_RECORDS", OracleDbType.Int32, ParameterDirection.Output);
                    cmd.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                    await cmd.ExecuteNonQueryAsync();

                    if (cmd.Parameters["p_TOTAL_RECORDS"].Value is OracleDecimal oracleDecimal && !oracleDecimal.IsNull)
                    {
                        totalRecords = oracleDecimal.ToInt32();
                    }

                    using (var reader = ((OracleRefCursor)cmd.Parameters["p_RESULT_CURSOR"].Value).GetDataReader())
                    {
                        while (await reader.ReadAsync())
                        {
                            rcms.Add(MapReaderToRcmMaster(reader));
                        }
                    }
                }
            }
            return (rcms, totalRecords);
        }

        // --- NEW METHOD ---
        // This method calls the set_rcm_status procedure.
        public async Task SetRcmStatusAsync(int sqNo, string newStatus, string updatedBy)
        {
            using (var conn = new OracleConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new OracleCommand("IFCO.RCM_MASTER_PKG.set_rcm_status", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_SQ_NO", OracleDbType.Int32, sqNo, ParameterDirection.Input);
                    cmd.Parameters.Add("p_STATUS", OracleDbType.NVarchar2, newStatus, ParameterDirection.Input);
                    cmd.Parameters.Add("p_UPDATED_BY", OracleDbType.NVarchar2, updatedBy, ParameterDirection.Input);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
        public async Task<List<RcmMaster>> GetRcmsForVerticalAsync(string verticalId)
        {
            var rcms = new List<RcmMaster>();
            using (var conn = new OracleConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new OracleCommand("IFCO.RCM_MASTER_PKG.get_rcms_for_vertical", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_VERTICAL_ID", OracleDbType.NVarchar2, verticalId, ParameterDirection.Input);
                    cmd.Parameters.Add("p_PAGE_NUMBER", OracleDbType.Int32, 1, ParameterDirection.Input);
                    cmd.Parameters.Add("p_PAGE_SIZE", OracleDbType.Int32, 5000, ParameterDirection.Input); // Fetch all for UI
                    cmd.Parameters.Add("p_TOTAL_RECORDS", OracleDbType.Int32, ParameterDirection.Output);
                    cmd.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            rcms.Add(new RcmMaster
                            {
                                SqNo = Convert.ToInt32(reader["SQ_NO"]),
                                RcmId = reader["RCM_ID"].ToString(),
                                RcmName = reader["RCM_NAME"].ToString(),
                                Status = reader["STATUS"].ToString(),
                            });
                        }
                    }
                }
            }
            return rcms;
        }

        // NEW: Gets all Verticals that a specific RCM is mapped to.
        public async Task<List<VerticalMaster>> GetVerticalsForRcmAsync(int rcmSqNo)
        {
            var verticals = new List<VerticalMaster>();
            using (var conn = new OracleConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new OracleCommand("IFCO.RCM_MASTER_PKG.get_verticals_for_rcm", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_RCM_SQ_NO", OracleDbType.Int32, rcmSqNo, ParameterDirection.Input);
                    cmd.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            verticals.Add(new VerticalMaster
                            {
                                SqNo = Convert.ToInt32(reader["SQ_NO"]),
                                VerticalId = reader["VERTICAL_ID"].ToString(),
                                VerticalName = reader["VERTICAL_NAME"].ToString(),
                                Status = reader["STATUS"].ToString()
                            });
                        }
                    }
                }
            }
            return verticals;
        }

        // MODIFIED: No longer takes a VerticalId.
        public async Task<int> AddRcmAsync(RcmMaster rcm, string createdBy)
        {
            using (var conn = new OracleConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new OracleCommand("IFCO.RCM_MASTER_PKG.add_rcm", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_RCM_ID", OracleDbType.NVarchar2, rcm.RcmId, ParameterDirection.Input);
                    cmd.Parameters.Add("p_RCM_NAME", OracleDbType.NVarchar2, rcm.RcmName, ParameterDirection.Input);
                    cmd.Parameters.Add("p_CREATED_BY", OracleDbType.NVarchar2, createdBy, ParameterDirection.Input);
                    cmd.Parameters.Add("p_SQ_NO", OracleDbType.Int32, ParameterDirection.Output);

                    await cmd.ExecuteNonQueryAsync();
                    return ((OracleDecimal)cmd.Parameters["p_SQ_NO"].Value).ToInt32();
                }
            }
        }

        // MODIFIED: No longer updates a VerticalId.
        public async Task UpdateRcmAsync(RcmMaster rcm, string updatedBy)
        {
            using (var conn = new OracleConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new OracleCommand("IFCO.RCM_MASTER_PKG.update_rcm", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_SQ_NO", OracleDbType.Int32, rcm.SqNo, ParameterDirection.Input);
                    cmd.Parameters.Add("p_RCM_NAME", OracleDbType.NVarchar2, rcm.RcmName, ParameterDirection.Input);
                    cmd.Parameters.Add("p_UPDATED_BY", OracleDbType.NVarchar2, updatedBy, ParameterDirection.Input);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        // NEW: Method to create a mapping.
        public async Task MapRcmToVerticalAsync(int rcmSqNo, string verticalId, string createdBy)
        {
            using (var conn = new OracleConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new OracleCommand("IFCO.RCM_MASTER_PKG.map_rcm_to_vertical", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_RCM_SQ_NO", OracleDbType.Int32, rcmSqNo, ParameterDirection.Input);
                    cmd.Parameters.Add("p_VERTICAL_ID", OracleDbType.NVarchar2, verticalId, ParameterDirection.Input);
                    cmd.Parameters.Add("p_CREATED_BY", OracleDbType.NVarchar2, createdBy, ParameterDirection.Input);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        // NEW: Method to remove a mapping.
        public async Task UnmapRcmFromVerticalAsync(int rcmSqNo, string verticalId)
        {
            using (var conn = new OracleConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new OracleCommand("IFCO.RCM_MASTER_PKG.unmap_rcm_from_vertical", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_RCM_SQ_NO", OracleDbType.Int32, rcmSqNo, ParameterDirection.Input);
                    cmd.Parameters.Add("p_VERTICAL_ID", OracleDbType.NVarchar2, verticalId, ParameterDirection.Input);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
        private RcmMaster MapReaderToRcmMaster(OracleDataReader reader)
        {
            return new RcmMaster
            {
                SqNo = Convert.ToInt32(reader["SQ_NO"]),
                RcmId = reader["RCM_ID"].ToString(),
                RcmName = reader["RCM_NAME"].ToString(),
                Status = reader["STATUS"].ToString(),
            };
        }

        private VerticalMaster MapReaderToVerticalMaster(OracleDataReader reader)
        {
            return new VerticalMaster
            {
                SqNo = Convert.ToInt32(reader["SQ_NO"]),
                VerticalId = reader["VERTICAL_ID"].ToString(),
                VerticalName = reader["VERTICAL_NAME"].ToString(),
                Status = reader["STATUS"].ToString()
                // Note: Map other audit columns if needed
            };
        }
        // Add this new method inside your OracleService class

        public async Task<List<VerticalMaster>> GetVerticalsAsync()
        {
            var verticals = new List<VerticalMaster>();
            using (var conn = new OracleConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new OracleCommand("IFCO.VERTICAL_MASTER_PKG.search_verticals", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_SEARCH_TERM", OracleDbType.NVarchar2, DBNull.Value, ParameterDirection.Input);
                    cmd.Parameters.Add("p_STATUS", OracleDbType.NVarchar2, "Active", ParameterDirection.Input); // We only want active verticals
                    cmd.Parameters.Add("p_PAGE_NUMBER", OracleDbType.Int32, 1, ParameterDirection.Input);
                    cmd.Parameters.Add("p_PAGE_SIZE", OracleDbType.Int32, 5000, ParameterDirection.Input); // Use a large page size to get all
                    cmd.Parameters.Add("p_TOTAL_RECORDS", OracleDbType.Int32, ParameterDirection.Output);
                    cmd.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            // This uses the helper method we added in the previous step
                            verticals.Add(MapReaderToVerticalMaster(reader));
                        }
                    }
                }
            }
            return verticals;
        }
        // File: Services/OracleService.cs

        // Add this new helper method to get all active consultants (for the dual listbox)
        public async Task<List<ConsultantMaster>> GetActiveConsultantsAsync()
        {
            var result = await SearchConsultantsAsync(consultantType: null, pageSize: 5000); // Get all consultants
            return result.Consultants.Where(c => c.Status == "Active").ToList();
        }

        // Add these new methods to call your new database procedures
        public async Task<List<ConsultantMaster>> GetConsultantsForRcmAsync(int rcmSqNo)
        {
            var consultants = new List<ConsultantMaster>();
            using (var conn = new OracleConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new OracleCommand("IFCO.RCM_MASTER_PKG.get_consultants_for_rcm", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_RCM_SQ_NO", OracleDbType.Int32, rcmSqNo, ParameterDirection.Input);
                    cmd.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            consultants.Add(new ConsultantMaster
                            {
                                SqNo = Convert.ToInt32(reader["SQ_NO"]),
                                ConsultantId = reader["CONSULTANT_ID"].ToString(),
                                ConsultantName = reader["CONSULTANT_NAME"].ToString(),
                                ConsultantType = reader["CONSULTANT_TYPE"].ToString()
                            });
                        }
                    }
                }
            }
            return consultants;
        }

        public async Task MapRcmToConsultantAsync(int rcmSqNo, int consultantSqNo, string createdBy)
        {
            using (var conn = new OracleConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new OracleCommand("IFCO.RCM_MASTER_PKG.map_rcm_to_consultant", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_RCM_SQ_NO", OracleDbType.Int32, rcmSqNo, ParameterDirection.Input);
                    cmd.Parameters.Add("p_CONSULTANT_SQ_NO", OracleDbType.Int32, consultantSqNo, ParameterDirection.Input);
                    cmd.Parameters.Add("p_CREATED_BY", OracleDbType.NVarchar2, createdBy, ParameterDirection.Input);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task UnmapRcmFromConsultantAsync(int rcmSqNo, int consultantSqNo)
        {
            using (var conn = new OracleConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new OracleCommand("IFCO.RCM_MASTER_PKG.unmap_rcm_from_consultant", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_RCM_SQ_NO", OracleDbType.Int32, rcmSqNo, ParameterDirection.Input);
                    cmd.Parameters.Add("p_CONSULTANT_SQ_NO", OracleDbType.Int32, consultantSqNo, ParameterDirection.Input);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
        #endregion
        #region Vertical User Master
        public async Task<(List<VerticalUserMaster> Users, int TotalRecords)> SearchUsersAsync(string? searchTerm = null, int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                var users = new List<VerticalUserMaster>();
                int totalRecords = 0;

                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.VERTICAL_USER_MASTER_PKG.search_users", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        command.Parameters.Add("p_SEARCH_TERM", OracleDbType.Varchar2, searchTerm, ParameterDirection.Input);
                        command.Parameters.Add("p_USER_TYPE", OracleDbType.NVarchar2, null, ParameterDirection.Input);
                        command.Parameters.Add("p_VERTICAL_ID", OracleDbType.NVarchar2, null, ParameterDirection.Input);
                        command.Parameters.Add("p_STATUS", OracleDbType.NVarchar2, null, ParameterDirection.Input);
                        command.Parameters.Add("p_PAGE_NUMBER", OracleDbType.Int32, pageNumber, ParameterDirection.Input);
                        command.Parameters.Add("p_PAGE_SIZE", OracleDbType.Int32, pageSize, ParameterDirection.Input);
                        command.Parameters.Add("p_TOTAL_RECORDS", OracleDbType.Int32, ParameterDirection.Output);
                        command.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                        await command.ExecuteNonQueryAsync();

                        totalRecords = Convert.ToInt32(command.Parameters["p_TOTAL_RECORDS"].Value.ToString());

                        using (var reader = ((OracleRefCursor)command.Parameters["p_RESULT_CURSOR"].Value).GetDataReader())
                        {
                            while (await reader.ReadAsync())
                            {
                                users.Add(new VerticalUserMaster
                                {
                                    SqNo = reader.GetInt32(reader.GetOrdinal("SQ_NO")),
                                    UserId = reader.GetString(reader.GetOrdinal("USER_ID")),
                                    UserName = reader.GetString(reader.GetOrdinal("USER_NAME")),
                                    UserType = reader.GetString(reader.GetOrdinal("USER_TYPE")),
                                    VerticalId = reader.IsDBNull(reader.GetOrdinal("VERTICAL_ID")) ? null : reader.GetString(reader.GetOrdinal("VERTICAL_ID")),
                                    Status = reader.GetString(reader.GetOrdinal("STATUS")),
                                    CreatedBy = reader.GetString(reader.GetOrdinal("CREATED_BY")),
                                    CreatedDate = reader.GetDateTime(reader.GetOrdinal("CREATED_DATE")),
                                    VerticalName = reader.IsDBNull(reader.GetOrdinal("VERTICAL_NAME")) ? null : reader.GetString(reader.GetOrdinal("VERTICAL_NAME"))
                                });
                            }
                        }
                    }
                }
                return (users, totalRecords);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calling method SearchUsersAsync");
                throw;
            }
        }

        // File: Services/OracleService.cs
        public async Task AddUserAsync(VerticalUserMaster user, string createdBy) // <-- MODIFIED: Added createdBy parameter
        {
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.VERTICAL_USER_MASTER_PKG.add_user", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        command.Parameters.Add("p_USER_ID", OracleDbType.NVarchar2, user.UserId, ParameterDirection.Input);
                        command.Parameters.Add("p_USER_NAME", OracleDbType.NVarchar2, user.UserName, ParameterDirection.Input);
                        command.Parameters.Add("p_USER_TYPE", OracleDbType.NVarchar2, user.UserType, ParameterDirection.Input);
                        command.Parameters.Add("p_VERTICAL_ID", OracleDbType.NVarchar2, user.VerticalId, ParameterDirection.Input);
                        command.Parameters.Add("p_CREATED_BY", OracleDbType.NVarchar2, createdBy, ParameterDirection.Input); // <-- MODIFIED: Use the parameter
                        command.Parameters.Add("p_SQ_NO", OracleDbType.Int32, ParameterDirection.Output);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calling method AddUserAsync");
                throw;
            }
        }
        // File: Services/OracleService.cs

        public async Task<VerticalUserMaster> GetUserBySqNoAsync(int sqNo)
        {
            VerticalUserMaster? user = null;
            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand("IFCO.VERTICAL_USER_MASTER_PKG.get_user_by_sq_no", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add("p_SQ_NO", OracleDbType.Int32, sqNo, ParameterDirection.Input);
                    command.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            user = new VerticalUserMaster
                            {
                                SqNo = reader.GetInt32(reader.GetOrdinal("SQ_NO")),
                                UserId = reader.GetString(reader.GetOrdinal("USER_ID")),
                                UserName = reader.GetString(reader.GetOrdinal("USER_NAME")),
                                UserType = reader.GetString(reader.GetOrdinal("USER_TYPE")),
                                VerticalId = reader.IsDBNull(reader.GetOrdinal("VERTICAL_ID")) ? null : reader.GetString(reader.GetOrdinal("VERTICAL_ID")),
                                Status = reader.GetString(reader.GetOrdinal("STATUS"))
                            };
                        }
                    }
                }
            }
            return user ?? throw new Exception($"User with SQ_NO {sqNo} not found.");
        }
        public async Task UpdateUserAsync(VerticalUserMaster user, string updatedBy)
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand("IFCO.VERTICAL_USER_MASTER_PKG.update_user", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add("p_SQ_NO", OracleDbType.Int32, user.SqNo, ParameterDirection.Input);
                    command.Parameters.Add("p_USER_NAME", OracleDbType.NVarchar2, user.UserName, ParameterDirection.Input);
                    command.Parameters.Add("p_USER_TYPE", OracleDbType.NVarchar2, user.UserType, ParameterDirection.Input);
                    command.Parameters.Add("p_VERTICAL_ID", OracleDbType.NVarchar2, user.VerticalId, ParameterDirection.Input);
                    command.Parameters.Add("p_UPDATED_BY", OracleDbType.NVarchar2, updatedBy, ParameterDirection.Input);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
        public async Task SetUserStatusAsync(int sqNo, string newStatus, string updatedBy)
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand("IFCO.VERTICAL_USER_MASTER_PKG.set_user_status", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add("p_SQ_NO", OracleDbType.Int32, sqNo, ParameterDirection.Input);
                    command.Parameters.Add("p_STATUS", OracleDbType.NVarchar2, newStatus, ParameterDirection.Input);
                    command.Parameters.Add("p_UPDATED_BY", OracleDbType.NVarchar2, updatedBy, ParameterDirection.Input);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }
        #endregion
        #region RCM Points and RCM Points File
        public async Task<List<RcmMaster>> GetActiveRcmsAsync()
        {
            var rcms = new List<RcmMaster>();
            using (var conn = new OracleConnection(_connectionString))
            {
                await conn.OpenAsync();
                // This is a simplified query to get all active RCMs for dropdowns.
                // In a large system, you might want to use a dedicated procedure.
                var sql = "SELECT SQ_NO, RCM_ID, RCM_NAME, STATUS FROM IFCO.RCM_MASTER WHERE STATUS = 'Active' ORDER BY RCM_NAME";
                using (var cmd = new OracleCommand(sql, conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            rcms.Add(new RcmMaster
                            {
                                SqNo = Convert.ToInt32(reader["SQ_NO"]),
                                RcmId = reader["RCM_ID"].ToString(),
                                RcmName = reader["RCM_NAME"].ToString(),
                                Status = reader["STATUS"].ToString(),
                            });
                        }
                    }
                }
            }
            return rcms;
        }

        public async Task<(List<RcmPointsMaster> Points, int TotalRecords)> SearchRcmPointsAsync(int rcmSqNo, string? searchTerm = null, string? status = null, int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                var points = new List<RcmPointsMaster>();
                int totalRecords = 0;

                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.RCM_POINTS_PKG.search_rcm_points", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        // NOTE: parameters are bound positionally (BindByName is not set on
                        // this command), so this MUST stay first to match p_SEARCH_TERM in
                        // the package signature. Previously always sent as null - the search
                        // box UI was never actually wired up to it.
                        command.Parameters.Add("p_SEARCH_TERM", OracleDbType.Varchar2, string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm, ParameterDirection.Input);
                        command.Parameters.Add("p_RCM_SQ_NO", OracleDbType.Int32, rcmSqNo, ParameterDirection.Input);
                        // MODIFIED: Pass the status parameter to the database
                        command.Parameters.Add("p_STATUS", OracleDbType.NVarchar2, status, ParameterDirection.Input);
                        command.Parameters.Add("p_PAGE_NUMBER", OracleDbType.Int32, pageNumber, ParameterDirection.Input);
                        command.Parameters.Add("p_PAGE_SIZE", OracleDbType.Int32, pageSize, ParameterDirection.Input);
                        command.Parameters.Add("p_TOTAL_RECORDS", OracleDbType.Int32, ParameterDirection.Output);
                        command.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                        await command.ExecuteNonQueryAsync();

                        if (command.Parameters["p_TOTAL_RECORDS"].Value is OracleDecimal oracleDecimal && !oracleDecimal.IsNull)
                        {
                            totalRecords = oracleDecimal.ToInt32();
                        }

                        using (var reader = ((OracleRefCursor)command.Parameters["p_RESULT_CURSOR"].Value).GetDataReader())
                        {
                            while (await reader.ReadAsync())
                            {
                                points.Add(new RcmPointsMaster
                                {
                                    SqNo = reader.GetInt32(reader.GetOrdinal("SQ_NO")),
                                    RcmSqNo = reader.GetInt32(reader.GetOrdinal("RCM_SQ_NO")),
                                    VerticalId = reader.IsDBNull(reader.GetOrdinal("VERTICAL_ID")) ? null : reader.GetString(reader.GetOrdinal("VERTICAL_ID")),
                                    Process = reader.IsDBNull(reader.GetOrdinal("PROCESS")) ? null : reader.GetString(reader.GetOrdinal("PROCESS")),
                                    SubProcess = reader.IsDBNull(reader.GetOrdinal("SUB_PROCESS")) ? null : reader.GetString(reader.GetOrdinal("SUB_PROCESS")),
                                    RiskDescription = reader.IsDBNull(reader.GetOrdinal("RISK_DESCRIPTION")) ? null : reader.GetString(reader.GetOrdinal("RISK_DESCRIPTION")),
                                    ControlActivity = reader.IsDBNull(reader.GetOrdinal("CONTROL_ACTIVITY")) ? null : reader.GetString(reader.GetOrdinal("CONTROL_ACTIVITY")),
                                    Status = reader.IsDBNull(reader.GetOrdinal("STATUS")) ? null : reader.GetString(reader.GetOrdinal("STATUS")),
                                    CreatedBy = reader.IsDBNull(reader.GetOrdinal("CREATED_BY")) ? null : reader.GetString(reader.GetOrdinal("CREATED_BY")),
                                    CreatedDate = reader.GetDateTime(reader.GetOrdinal("CREATED_DATE")),
                                    RcmName = reader.IsDBNull(reader.GetOrdinal("RCM_NAME")) ? null : reader.GetString(reader.GetOrdinal("RCM_NAME")),
                                    VerticalName = reader.IsDBNull(reader.GetOrdinal("VERTICAL_NAME")) ? null : reader.GetString(reader.GetOrdinal("VERTICAL_NAME")),
                                });
                            }
                        }
                    }
                }
                return (points, totalRecords);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calling method SearchRcmPointsAsync");
                throw;
            }
        }

        public async Task<int> AddRcmPointAsync(RcmPointsMaster point, string createdBy)
        {
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.RCM_POINTS_PKG.add_rcm_point", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        // This now maps all the fields from your form
                        command.Parameters.Add("p_RCM_SQ_NO", OracleDbType.Int32, point.RcmSqNo, ParameterDirection.Input);
                        command.Parameters.Add("p_VERTICAL_ID", OracleDbType.NVarchar2, point.VerticalId, ParameterDirection.Input);
                        command.Parameters.Add("p_PROCESS", OracleDbType.NVarchar2, point.Process, ParameterDirection.Input);
                        command.Parameters.Add("p_SUB_PROCESS", OracleDbType.NVarchar2, point.SubProcess, ParameterDirection.Input);
                        command.Parameters.Add("p_RISK_DESCRIPTION", OracleDbType.NVarchar2, point.RiskDescription, ParameterDirection.Input);
                        command.Parameters.Add("p_IMPACT_ON_OCCURRENCE", OracleDbType.NVarchar2, point.ImpactOnOccurrence, ParameterDirection.Input);
                        command.Parameters.Add("p_SCORE_OF_IMPACT_ON_OCCURRENCE", OracleDbType.NVarchar2, point.ScoreOfImpactOnOccurrence, ParameterDirection.Input);
                        command.Parameters.Add("p_PROBABILITY_OF_OCCURRENCE", OracleDbType.NVarchar2, point.ProbabilityOfOccurrence, ParameterDirection.Input);
                        command.Parameters.Add("p_SCORE_OF_PROBABILITY_OF_OCCURRENCE", OracleDbType.NVarchar2, point.ScoreOfProbabilityOfOccurrence, ParameterDirection.Input);
                        command.Parameters.Add("p_RISK_RATING_H_J", OracleDbType.NVarchar2, point.RiskRatingHJ, ParameterDirection.Input);
                        command.Parameters.Add("p_RISK_CATEGORY", OracleDbType.NVarchar2, point.RiskCategory, ParameterDirection.Input);
                        command.Parameters.Add("p_SAD_SCHEDULE_NO", OracleDbType.NVarchar2, point.SadScheduleNo, ParameterDirection.Input);
                        command.Parameters.Add("p_SAD_NAME", OracleDbType.NVarchar2, point.SadName, ParameterDirection.Input);
                        command.Parameters.Add("p_EXISTENCE_OCCURRENCE", OracleDbType.NVarchar2, point.ExistenceOccurrence, ParameterDirection.Input);
                        command.Parameters.Add("p_COMPLETENESS_CUT_OFF", OracleDbType.NVarchar2, point.CompletenessCutOff, ParameterDirection.Input);
                        command.Parameters.Add("p_RIGHTS_OBLIGATIONS", OracleDbType.NVarchar2, point.RightsObligations, ParameterDirection.Input);
                        command.Parameters.Add("p_VALUATION_ALLOCATION", OracleDbType.NVarchar2, point.ValuationAllocation, ParameterDirection.Input);
                        command.Parameters.Add("p_ACCURACY_CLASSIFICATION", OracleDbType.NVarchar2, point.AccuracyClassification, ParameterDirection.Input);
                        command.Parameters.Add("p_PRESENTATION_DISCLOSURE", OracleDbType.NVarchar2, point.PresentationDisclosure, ParameterDirection.Input);
                        command.Parameters.Add("p_CONTROL_ACTIVITY", OracleDbType.NVarchar2, point.ControlActivity, ParameterDirection.Input); // Note: Your requirement mentioned "Control Department" mapping to this.
                        command.Parameters.Add("p_CONTROL_DEPT", OracleDbType.NVarchar2, point.ControlDept, ParameterDirection.Input);
                        command.Parameters.Add("p_DESIGNATION", OracleDbType.NVarchar2, point.Designation, ParameterDirection.Input);
                        command.Parameters.Add("p_COMMON_CENTRALISED_CONTROL", OracleDbType.NVarchar2, point.CommonCentralisedControl, ParameterDirection.Input);
                        command.Parameters.Add("p_CONTROL_TYPE", OracleDbType.NVarchar2, point.ControlType, ParameterDirection.Input);
                        command.Parameters.Add("p_CONTROL_TYPE_SCORE", OracleDbType.NVarchar2, point.ControlTypeScore, ParameterDirection.Input);
                        command.Parameters.Add("p_LEVEL_OF_AUTOMATION", OracleDbType.NVarchar2, point.LevelOfAutomation, ParameterDirection.Input);
                        command.Parameters.Add("p_CONTROL_AUTOMATION_SCORE", OracleDbType.NVarchar2, point.ControlAutomationScore, ParameterDirection.Input);
                        command.Parameters.Add("p_CONTROL_STRENGTH_SCORE", OracleDbType.NVarchar2, point.ControlStrengthScore, ParameterDirection.Input);
                        command.Parameters.Add("p_CONTROL_STRENGTH", OracleDbType.NVarchar2, point.ControlStrength, ParameterDirection.Input);
                        command.Parameters.Add("p_SCORE_FOR_KEY_NON_KEY", OracleDbType.NVarchar2, point.ScoreForKeyNonKey, ParameterDirection.Input);
                        command.Parameters.Add("p_KEY_CONTROL_NON_KEY_CONTROL", OracleDbType.NVarchar2, point.KeyControlNonKeyControl, ParameterDirection.Input);
                        command.Parameters.Add("p_FREQUENCY_OF_CONTROL", OracleDbType.NVarchar2, point.FrequencyOfControl, ParameterDirection.Input);
                        command.Parameters.Add("p_DESIGN_GAP_YES_NO", OracleDbType.NVarchar2, point.DesignGapYesNo, ParameterDirection.Input);
                        command.Parameters.Add("p_RECOMMENDATION_REMEDIATION_PLAN", OracleDbType.NVarchar2, point.RecommendationRemediationPlan, ParameterDirection.Input);
                        command.Parameters.Add("p_MANAGEMENT_RESPONSE", OracleDbType.NVarchar2, point.ManagementResponse, ParameterDirection.Input);
                        command.Parameters.Add("p_TESTING_PROCEDURE", OracleDbType.NVarchar2, point.TestingProcedure, ParameterDirection.Input);
                        command.Parameters.Add("p_NO_OF_SAMPLES_TESTED", OracleDbType.NVarchar2, point.NoOfSamplesTested, ParameterDirection.Input);
                        command.Parameters.Add("p_NATURE_OF_TESTS_CARRIED_OUT", OracleDbType.NVarchar2, point.NatureOfTestsCarriedOut, ParameterDirection.Input);
                        command.Parameters.Add("p_OPERATING_EFFECTIVENESS", OracleDbType.NVarchar2, point.OperatingEffectiveness, ParameterDirection.Input);
                        command.Parameters.Add("p_CONTROL_ACTIVITY_EVIDENCE", OracleDbType.NVarchar2, point.ControlActivityEvidence, ParameterDirection.Input);
                        command.Parameters.Add("p_COMMENT_IF_ANY", OracleDbType.NVarchar2, point.CommentIfAny, ParameterDirection.Input);
                        command.Parameters.Add("p_MANAGEMENT_REPLY", OracleDbType.NVarchar2, point.ManagementReply, ParameterDirection.Input);
                        command.Parameters.Add("p_REJECTION_REASON", OracleDbType.NVarchar2, point.RejectionReason, ParameterDirection.Input);
                        command.Parameters.Add("p_ADMIN_REPLY", OracleDbType.NVarchar2, point.AdminReply, ParameterDirection.Input);
                        command.Parameters.Add("p_STATUS", OracleDbType.NVarchar2, point.Status, ParameterDirection.Input);
                        command.Parameters.Add("p_CREATED_BY", OracleDbType.NVarchar2, createdBy, ParameterDirection.Input); // Use the captured user ID

                        command.Parameters.Add("p_SQ_NO", OracleDbType.Int32, ParameterDirection.Output);

                        await command.ExecuteNonQueryAsync();
                        return ((OracleDecimal)command.Parameters["p_SQ_NO"].Value).ToInt32();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calling method AddRcmPointAsync");
                throw;
            }
        }

        public async Task<List<RcmPointFilesMaster>> GetFilesForPointAsync(int rcmPointSqNo)
        {
            try
            {
                var files = new List<RcmPointFilesMaster>();
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.RCM_POINTS_PKG.get_files_for_point", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("p_RCM_POINT_SQ_NO", OracleDbType.Int32, rcmPointSqNo, ParameterDirection.Input);
                        command.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                        await command.ExecuteNonQueryAsync();

                        using (var reader = ((OracleRefCursor)command.Parameters["p_RESULT_CURSOR"].Value).GetDataReader())
                        {
                            while (await reader.ReadAsync())
                            {
                                files.Add(new RcmPointFilesMaster
                                {
                                    FileSqNo = reader.GetInt32(reader.GetOrdinal("FILE_SQ_NO")),
                                    RcmPointSqNo = reader.GetInt32(reader.GetOrdinal("RCM_POINT_SQ_NO")),
                                    FileName = reader.GetString(reader.GetOrdinal("FILE_NAME")),
                                    FileType = reader.GetString(reader.GetOrdinal("FILE_TYPE")),
                                    FileSizeBytes = reader.GetInt64(reader.GetOrdinal("FILE_SIZE_BYTES")),
                                    UploadedBy = reader.GetString(reader.GetOrdinal("UPLOADED_BY")),
                                    UploadedDate = reader.GetDateTime(reader.GetOrdinal("UPLOADED_DATE"))
                                });
                            }
                        }
                    }
                }
                return files;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calling method GetFilesForPointAsync");
                throw;
            }
        }
        public async Task AddFileAsync(RcmPointFilesMaster file)
        {
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.RCM_POINTS_PKG.add_file", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        command.Parameters.Add("p_RCM_POINT_SQ_NO", OracleDbType.Int32, file.RcmPointSqNo, ParameterDirection.Input);
                        command.Parameters.Add("p_FILE_NAME", OracleDbType.NVarchar2, file.FileName, ParameterDirection.Input);
                        command.Parameters.Add("p_FILE_TYPE", OracleDbType.NVarchar2, file.FileType, ParameterDirection.Input);
                        command.Parameters.Add("p_FILE_SIZE_BYTES", OracleDbType.Int64, file.FileSizeBytes, ParameterDirection.Input);
                        command.Parameters.Add("p_FILE_DATA", OracleDbType.Blob, file.FileData, ParameterDirection.Input);
                        command.Parameters.Add("p_UPLOADED_BY", OracleDbType.NVarchar2, "SYSTEM", ParameterDirection.Input);
                        command.Parameters.Add("p_FILE_SQ_NO", OracleDbType.Int32, ParameterDirection.Output);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calling method AddFileAsync");
                throw;
            }
        }
        public async Task DeleteFileAsync(int fileSqNo)
        {
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.RCM_POINTS_PKG.delete_file", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("p_FILE_SQ_NO", OracleDbType.Int32, fileSqNo, ParameterDirection.Input);
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calling method DeleteFileAsync");
                throw;
            }
        }

        public async Task<RcmPointsMaster> GetRcmPointByIdAsync(int sqNo)
        {
            try
            {
                RcmPointsMaster? point = null;
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.RCM_POINTS_PKG.get_rcm_point_by_id", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("p_SQ_NO", OracleDbType.Int32, sqNo, ParameterDirection.Input);
                        command.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                        await command.ExecuteNonQueryAsync();

                        using (var reader = ((OracleRefCursor)command.Parameters["p_RESULT_CURSOR"].Value).GetDataReader())
                        {
                            if (await reader.ReadAsync())
                            {
                                // MODIFIED: This now reads all columns for the edit form
                                point = new RcmPointsMaster
                                {
                                    SqNo = reader.GetInt32(reader.GetOrdinal("SQ_NO")),
                                    RcmSqNo = reader.GetInt32(reader.GetOrdinal("RCM_SQ_NO")),
                                    VerticalId = reader.GetString(reader.GetOrdinal("VERTICAL_ID")),
                                    Process = reader.IsDBNull(reader.GetOrdinal("PROCESS")) ? null : reader.GetString(reader.GetOrdinal("PROCESS")),
                                    SubProcess = reader.IsDBNull(reader.GetOrdinal("SUB_PROCESS")) ? null : reader.GetString(reader.GetOrdinal("SUB_PROCESS")),
                                    RiskDescription = reader.IsDBNull(reader.GetOrdinal("RISK_DESCRIPTION")) ? null : reader.GetString(reader.GetOrdinal("RISK_DESCRIPTION")),
                                    ImpactOnOccurrence = reader.IsDBNull(reader.GetOrdinal("IMPACT_ON_OCCURRENCE")) ? null : reader.GetString(reader.GetOrdinal("IMPACT_ON_OCCURRENCE")),
                                    ScoreOfImpactOnOccurrence = reader.IsDBNull(reader.GetOrdinal("SCORE_OF_IMPACT_ON_OCCURRENCE")) ? null : reader.GetString(reader.GetOrdinal("SCORE_OF_IMPACT_ON_OCCURRENCE")),
                                    ProbabilityOfOccurrence = reader.IsDBNull(reader.GetOrdinal("PROBABILITY_OF_OCCURRENCE")) ? null : reader.GetString(reader.GetOrdinal("PROBABILITY_OF_OCCURRENCE")),
                                    ScoreOfProbabilityOfOccurrence = reader.IsDBNull(reader.GetOrdinal("SCORE_OF_PROBABILITY_OF_OCCURRENCE")) ? null : reader.GetString(reader.GetOrdinal("SCORE_OF_PROBABILITY_OF_OCCURRENCE")),
                                    RiskRatingHJ = reader.IsDBNull(reader.GetOrdinal("RISK_RATING_H_J")) ? null : reader.GetString(reader.GetOrdinal("RISK_RATING_H_J")),
                                    RiskCategory = reader.IsDBNull(reader.GetOrdinal("RISK_CATEGORY")) ? null : reader.GetString(reader.GetOrdinal("RISK_CATEGORY")),
                                    SadScheduleNo = reader.IsDBNull(reader.GetOrdinal("SAD_SCHEDULE_NO")) ? null : reader.GetString(reader.GetOrdinal("SAD_SCHEDULE_NO")),
                                    SadName = reader.IsDBNull(reader.GetOrdinal("SAD_NAME")) ? null : reader.GetString(reader.GetOrdinal("SAD_NAME")),
                                    ExistenceOccurrence = reader.IsDBNull(reader.GetOrdinal("EXISTENCE_OCCURRENCE")) ? null : reader.GetString(reader.GetOrdinal("EXISTENCE_OCCURRENCE")),
                                    CompletenessCutOff = reader.IsDBNull(reader.GetOrdinal("COMPLETENESS_CUT_OFF")) ? null : reader.GetString(reader.GetOrdinal("COMPLETENESS_CUT_OFF")),
                                    RightsObligations = reader.IsDBNull(reader.GetOrdinal("RIGHTS_OBLIGATIONS")) ? null : reader.GetString(reader.GetOrdinal("RIGHTS_OBLIGATIONS")),
                                    ValuationAllocation = reader.IsDBNull(reader.GetOrdinal("VALUATION_ALLOCATION")) ? null : reader.GetString(reader.GetOrdinal("VALUATION_ALLOCATION")),
                                    AccuracyClassification = reader.IsDBNull(reader.GetOrdinal("ACCURACY_CLASSIFICATION")) ? null : reader.GetString(reader.GetOrdinal("ACCURACY_CLASSIFICATION")),
                                    PresentationDisclosure = reader.IsDBNull(reader.GetOrdinal("PRESENTATION_DISCLOSURE")) ? null : reader.GetString(reader.GetOrdinal("PRESENTATION_DISCLOSURE")),
                                    ControlActivity = reader.IsDBNull(reader.GetOrdinal("CONTROL_ACTIVITY")) ? null : reader.GetString(reader.GetOrdinal("CONTROL_ACTIVITY")),
                                    ControlDept = reader.IsDBNull(reader.GetOrdinal("CONTROL_DEPT")) ? null : reader.GetString(reader.GetOrdinal("CONTROL_DEPT")),
                                    Designation = reader.IsDBNull(reader.GetOrdinal("DESIGNATION")) ? null : reader.GetString(reader.GetOrdinal("DESIGNATION")),
                                    CommonCentralisedControl = reader.IsDBNull(reader.GetOrdinal("COMMON_CENTRALISED_CONTROL")) ? null : reader.GetString(reader.GetOrdinal("COMMON_CENTRALISED_CONTROL")),
                                    ControlType = reader.IsDBNull(reader.GetOrdinal("CONTROL_TYPE")) ? null : reader.GetString(reader.GetOrdinal("CONTROL_TYPE")),
                                    ControlTypeScore = reader.IsDBNull(reader.GetOrdinal("CONTROL_TYPE_SCORE")) ? null : reader.GetString(reader.GetOrdinal("CONTROL_TYPE_SCORE")),
                                    LevelOfAutomation = reader.IsDBNull(reader.GetOrdinal("LEVEL_OF_AUTOMATION")) ? null : reader.GetString(reader.GetOrdinal("LEVEL_OF_AUTOMATION")),
                                    ControlAutomationScore = reader.IsDBNull(reader.GetOrdinal("CONTROL_AUTOMATION_SCORE")) ? null : reader.GetString(reader.GetOrdinal("CONTROL_AUTOMATION_SCORE")),
                                    ControlStrengthScore = reader.IsDBNull(reader.GetOrdinal("CONTROL_STRENGTH_SCORE")) ? null : reader.GetString(reader.GetOrdinal("CONTROL_STRENGTH_SCORE")),
                                    ControlStrength = reader.IsDBNull(reader.GetOrdinal("CONTROL_STRENGTH")) ? null : reader.GetString(reader.GetOrdinal("CONTROL_STRENGTH")),
                                    ScoreForKeyNonKey = reader.IsDBNull(reader.GetOrdinal("SCORE_FOR_KEY_NON_KEY")) ? null : reader.GetString(reader.GetOrdinal("SCORE_FOR_KEY_NON_KEY")),
                                    KeyControlNonKeyControl = reader.IsDBNull(reader.GetOrdinal("KEY_CONTROL_NON_KEY_CONTROL")) ? null : reader.GetString(reader.GetOrdinal("KEY_CONTROL_NON_KEY_CONTROL")),
                                    FrequencyOfControl = reader.IsDBNull(reader.GetOrdinal("FREQUENCY_OF_CONTROL")) ? null : reader.GetString(reader.GetOrdinal("FREQUENCY_OF_CONTROL")),
                                    DesignGapYesNo = reader.IsDBNull(reader.GetOrdinal("DESIGN_GAP_YES_NO")) ? null : reader.GetString(reader.GetOrdinal("DESIGN_GAP_YES_NO")),
                                    RecommendationRemediationPlan = reader.IsDBNull(reader.GetOrdinal("RECOMMENDATION_REMEDIATION_PLAN")) ? null : reader.GetString(reader.GetOrdinal("RECOMMENDATION_REMEDIATION_PLAN")),
                                    ManagementResponse = reader.IsDBNull(reader.GetOrdinal("MANAGEMENT_RESPONSE")) ? null : reader.GetString(reader.GetOrdinal("MANAGEMENT_RESPONSE")),
                                    TestingProcedure = reader.IsDBNull(reader.GetOrdinal("TESTING_PROCEDURE")) ? null : reader.GetString(reader.GetOrdinal("TESTING_PROCEDURE")),
                                    NoOfSamplesTested = reader.IsDBNull(reader.GetOrdinal("NO_OF_SAMPLES_TESTED")) ? null : reader.GetString(reader.GetOrdinal("NO_OF_SAMPLES_TESTED")),
                                    NatureOfTestsCarriedOut = reader.IsDBNull(reader.GetOrdinal("NATURE_OF_TESTS_CARRIED_OUT")) ? null : reader.GetString(reader.GetOrdinal("NATURE_OF_TESTS_CARRIED_OUT")),
                                    OperatingEffectiveness = reader.IsDBNull(reader.GetOrdinal("OPERATING_EFFECTIVENESS")) ? null : reader.GetString(reader.GetOrdinal("OPERATING_EFFECTIVENESS")),
                                    ControlActivityEvidence = reader.IsDBNull(reader.GetOrdinal("CONTROL_ACTIVITY_EVIDENCE")) ? null : reader.GetString(reader.GetOrdinal("CONTROL_ACTIVITY_EVIDENCE")),
                                    CommentIfAny = reader.IsDBNull(reader.GetOrdinal("COMMENT_IF_ANY")) ? null : reader.GetString(reader.GetOrdinal("COMMENT_IF_ANY")),
                                    ManagementReply = reader.IsDBNull(reader.GetOrdinal("MANAGEMENT_REPLY")) ? null : reader.GetString(reader.GetOrdinal("MANAGEMENT_REPLY")),
                                    RejectionReason = reader.IsDBNull(reader.GetOrdinal("REJECTION_REASON")) ? null : reader.GetString(reader.GetOrdinal("REJECTION_REASON")),
                                    AdminReply = reader.IsDBNull(reader.GetOrdinal("ADMIN_REPLY")) ? null : reader.GetString(reader.GetOrdinal("ADMIN_REPLY")),
                                    Status = reader.GetString(reader.GetOrdinal("STATUS")),
                                    LastEditedByRole = reader.IsDBNull(reader.GetOrdinal("LAST_EDITED_BY_ROLE")) ? null : reader.GetString(reader.GetOrdinal("LAST_EDITED_BY_ROLE")),
                                    CheckerRemarks = reader.IsDBNull(reader.GetOrdinal("CHECKER_REMARKS")) ? null : reader.GetString(reader.GetOrdinal("CHECKER_REMARKS"))
                                };
                            }
                        }
                    }
                }
                return point ?? throw new Exception("RCM Point not found.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calling method GetRcmPointByIdAsync");
                throw;
            }
        }

        public async Task UpdateRcmPointAsync(RcmPointsMaster point, string updatedBy, string editedByRole, string? checkerRemarks = null)
        {
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.RCM_POINTS_PKG.update_rcm_point", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;

                        // This now maps all the fields from your form
                        command.Parameters.Add("p_SQ_NO", OracleDbType.Int32, point.SqNo, ParameterDirection.Input);
                        command.Parameters.Add("p_PROCESS", OracleDbType.NVarchar2, point.Process, ParameterDirection.Input);
                        command.Parameters.Add("p_SUB_PROCESS", OracleDbType.NVarchar2, point.SubProcess, ParameterDirection.Input);
                        command.Parameters.Add("p_RISK_DESCRIPTION", OracleDbType.NVarchar2, point.RiskDescription, ParameterDirection.Input);
                        command.Parameters.Add("p_IMPACT_ON_OCCURRENCE", OracleDbType.NVarchar2, point.ImpactOnOccurrence, ParameterDirection.Input);
                        command.Parameters.Add("p_SCORE_OF_IMPACT_ON_OCCURRENCE", OracleDbType.NVarchar2, point.ScoreOfImpactOnOccurrence, ParameterDirection.Input);
                        command.Parameters.Add("p_PROBABILITY_OF_OCCURRENCE", OracleDbType.NVarchar2, point.ProbabilityOfOccurrence, ParameterDirection.Input);
                        command.Parameters.Add("p_SCORE_OF_PROBABILITY_OF_OCCURRENCE", OracleDbType.NVarchar2, point.ScoreOfProbabilityOfOccurrence, ParameterDirection.Input);
                        command.Parameters.Add("p_RISK_RATING_H_J", OracleDbType.NVarchar2, point.RiskRatingHJ, ParameterDirection.Input);
                        command.Parameters.Add("p_RISK_CATEGORY", OracleDbType.NVarchar2, point.RiskCategory, ParameterDirection.Input);
                        command.Parameters.Add("p_SAD_SCHEDULE_NO", OracleDbType.NVarchar2, point.SadScheduleNo, ParameterDirection.Input);
                        command.Parameters.Add("p_SAD_NAME", OracleDbType.NVarchar2, point.SadName, ParameterDirection.Input);
                        command.Parameters.Add("p_EXISTENCE_OCCURRENCE", OracleDbType.NVarchar2, point.ExistenceOccurrence, ParameterDirection.Input);
                        command.Parameters.Add("p_COMPLETENESS_CUT_OFF", OracleDbType.NVarchar2, point.CompletenessCutOff, ParameterDirection.Input);
                        command.Parameters.Add("p_RIGHTS_OBLIGATIONS", OracleDbType.NVarchar2, point.RightsObligations, ParameterDirection.Input);
                        command.Parameters.Add("p_VALUATION_ALLOCATION", OracleDbType.NVarchar2, point.ValuationAllocation, ParameterDirection.Input);
                        command.Parameters.Add("p_ACCURACY_CLASSIFICATION", OracleDbType.NVarchar2, point.AccuracyClassification, ParameterDirection.Input);
                        command.Parameters.Add("p_PRESENTATION_DISCLOSURE", OracleDbType.NVarchar2, point.PresentationDisclosure, ParameterDirection.Input);
                        command.Parameters.Add("p_CONTROL_ACTIVITY", OracleDbType.NVarchar2, point.ControlActivity, ParameterDirection.Input);
                        command.Parameters.Add("p_CONTROL_DEPT", OracleDbType.NVarchar2, point.ControlDept, ParameterDirection.Input);
                        command.Parameters.Add("p_DESIGNATION", OracleDbType.NVarchar2, point.Designation, ParameterDirection.Input);
                        command.Parameters.Add("p_COMMON_CENTRALISED_CONTROL", OracleDbType.NVarchar2, point.CommonCentralisedControl, ParameterDirection.Input);
                        command.Parameters.Add("p_CONTROL_TYPE", OracleDbType.NVarchar2, point.ControlType, ParameterDirection.Input);
                        command.Parameters.Add("p_CONTROL_TYPE_SCORE", OracleDbType.NVarchar2, point.ControlTypeScore, ParameterDirection.Input);
                        command.Parameters.Add("p_LEVEL_OF_AUTOMATION", OracleDbType.NVarchar2, point.LevelOfAutomation, ParameterDirection.Input);
                        command.Parameters.Add("p_CONTROL_AUTOMATION_SCORE", OracleDbType.NVarchar2, point.ControlAutomationScore, ParameterDirection.Input);
                        command.Parameters.Add("p_CONTROL_STRENGTH_SCORE", OracleDbType.NVarchar2, point.ControlStrengthScore, ParameterDirection.Input);
                        command.Parameters.Add("p_CONTROL_STRENGTH", OracleDbType.NVarchar2, point.ControlStrength, ParameterDirection.Input);
                        command.Parameters.Add("p_SCORE_FOR_KEY_NON_KEY", OracleDbType.NVarchar2, point.ScoreForKeyNonKey, ParameterDirection.Input);
                        command.Parameters.Add("p_KEY_CONTROL_NON_KEY_CONTROL", OracleDbType.NVarchar2, point.KeyControlNonKeyControl, ParameterDirection.Input);
                        command.Parameters.Add("p_FREQUENCY_OF_CONTROL", OracleDbType.NVarchar2, point.FrequencyOfControl, ParameterDirection.Input);
                        command.Parameters.Add("p_DESIGN_GAP_YES_NO", OracleDbType.NVarchar2, point.DesignGapYesNo, ParameterDirection.Input);
                        command.Parameters.Add("p_RECOMMENDATION_REMEDIATION_PLAN", OracleDbType.NVarchar2, point.RecommendationRemediationPlan, ParameterDirection.Input);
                        command.Parameters.Add("p_MANAGEMENT_RESPONSE", OracleDbType.NVarchar2, point.ManagementResponse, ParameterDirection.Input);
                        command.Parameters.Add("p_TESTING_PROCEDURE", OracleDbType.NVarchar2, point.TestingProcedure, ParameterDirection.Input);
                        command.Parameters.Add("p_NO_OF_SAMPLES_TESTED", OracleDbType.NVarchar2, point.NoOfSamplesTested, ParameterDirection.Input);
                        command.Parameters.Add("p_NATURE_OF_TESTS_CARRIED_OUT", OracleDbType.NVarchar2, point.NatureOfTestsCarriedOut, ParameterDirection.Input);
                        command.Parameters.Add("p_OPERATING_EFFECTIVENESS", OracleDbType.NVarchar2, point.OperatingEffectiveness, ParameterDirection.Input);
                        command.Parameters.Add("p_CONTROL_ACTIVITY_EVIDENCE", OracleDbType.NVarchar2, point.ControlActivityEvidence, ParameterDirection.Input);
                        command.Parameters.Add("p_COMMENT_IF_ANY", OracleDbType.NVarchar2, point.CommentIfAny, ParameterDirection.Input);
                        command.Parameters.Add("p_MANAGEMENT_REPLY", OracleDbType.NVarchar2, point.ManagementReply, ParameterDirection.Input);
                        command.Parameters.Add("p_REJECTION_REASON", OracleDbType.NVarchar2, point.RejectionReason, ParameterDirection.Input);
                        command.Parameters.Add("p_ADMIN_REPLY", OracleDbType.NVarchar2, point.AdminReply, ParameterDirection.Input);
                        command.Parameters.Add("p_UPDATED_BY", OracleDbType.NVarchar2, updatedBy, ParameterDirection.Input); // Use the captured user ID
                        // NEW (Point 2): must stay positioned after p_UPDATED_BY to match the package signature
                        command.Parameters.Add("p_LAST_EDITED_BY_ROLE", OracleDbType.NVarchar2, editedByRole, ParameterDirection.Input);
                        command.Parameters.Add("p_CHECKER_REMARKS", OracleDbType.NVarchar2, string.IsNullOrWhiteSpace(checkerRemarks) ? null : checkerRemarks, ParameterDirection.Input);

                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calling method UpdateRcmPointAsync");
                throw;
            }
        }

        public async Task<ConsultantMaster?> GetConsultantByLoginIdAsync(string loginId)
        {
            try
            {
                ConsultantMaster? consultant = null;
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.CONSULTANT_MASTER_PKG.get_consultant_by_consultant_id", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("p_CONSULTANT_ID", OracleDbType.NVarchar2, loginId, ParameterDirection.Input);
                        command.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                        await command.ExecuteNonQueryAsync();

                        using (var reader = ((OracleRefCursor)command.Parameters["p_RESULT_CURSOR"].Value).GetDataReader())
                        {
                            if (await reader.ReadAsync())
                            {
                                consultant = new ConsultantMaster
                                {
                                    SqNo = reader.GetInt32(reader.GetOrdinal("SQ_NO")),
                                    ConsultantId = reader.GetString(reader.GetOrdinal("CONSULTANT_ID")),
                                    ConsultantName = reader.GetString(reader.GetOrdinal("CONSULTANT_NAME")),
                                    ConsultantType = reader.GetString(reader.GetOrdinal("CONSULTANT_TYPE")),
                                    MobileNumber = reader.GetString(reader.GetOrdinal("MOBILE_NUMBER")),
                                    EmailId = reader.IsDBNull(reader.GetOrdinal("EMAIL_ID")) ? null : reader.GetString(reader.GetOrdinal("EMAIL_ID")),
                                    Status = reader.GetString(reader.GetOrdinal("STATUS")),
                                };
                            }
                        }
                    }
                }
                return consultant;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calling method GetConsultantByLoginIdAsync");
                throw;
            }
        }
        public async Task<VerticalUserMaster?> GetVerticalUserByLoginIdAsync(string loginId)
        {
            try
            {
                VerticalUserMaster? user = null;
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.VERTICAL_USER_MASTER_PKG.get_user_by_user_id", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("p_USER_ID", OracleDbType.NVarchar2, loginId, ParameterDirection.Input);
                        command.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                        await command.ExecuteNonQueryAsync();

                        using (var reader = ((OracleRefCursor)command.Parameters["p_RESULT_CURSOR"].Value).GetDataReader())
                        {
                            if (await reader.ReadAsync())
                            {
                                user = new VerticalUserMaster
                                {
                                    SqNo = reader.GetInt32(reader.GetOrdinal("SQ_NO")),
                                    UserId = reader.GetString(reader.GetOrdinal("USER_ID")),
                                    UserName = reader.GetString(reader.GetOrdinal("USER_NAME")),
                                    UserType = reader.GetString(reader.GetOrdinal("USER_TYPE")),
                                    VerticalId = reader.IsDBNull(reader.GetOrdinal("VERTICAL_ID")) ? null : reader.GetString(reader.GetOrdinal("VERTICAL_ID")),
                                    Status = reader.GetString(reader.GetOrdinal("STATUS")),
                                };
                            }
                        }
                    }
                }
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calling method GetVerticalUserByLoginIdAsync");
                throw;
            }
        }
        public async Task SetRcmPointStatusAsync(int sqNo, string newStatus, string updatedBy)
        {
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.RCM_POINTS_PKG.set_rcm_point_status", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("p_SQ_NO", OracleDbType.Int32, sqNo, ParameterDirection.Input);
                        command.Parameters.Add("p_STATUS", OracleDbType.NVarchar2, newStatus, ParameterDirection.Input);
                        command.Parameters.Add("p_UPDATED_BY", OracleDbType.NVarchar2, updatedBy, ParameterDirection.Input);
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calling method SetRcmPointStatusAsync");
                throw;
            }
        }
        // File: IFCO.WEB/Services/OracleService.cs

        // NEW METHOD: Fetches the list of Verticals mapped to a specific RCM.
        public async Task<List<VerticalMaster>> GetMappedVerticalsForRcmAsync(int rcmSqNo)
        {
            var verticals = new List<VerticalMaster>();
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.RCM_POINTS_PKG.get_mapped_verticals_for_rcm", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("p_RCM_SQ_NO", OracleDbType.Int32, rcmSqNo, ParameterDirection.Input);
                        command.Parameters.Add("p_STATUS", OracleDbType.NVarchar2, "Active", ParameterDirection.Input); // Only fetch active verticals
                        command.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                        await command.ExecuteNonQueryAsync();

                        using (var reader = ((OracleRefCursor)command.Parameters["p_RESULT_CURSOR"].Value).GetDataReader())
                        {
                            while (await reader.ReadAsync())
                            {
                                verticals.Add(new VerticalMaster
                                {
                                    VerticalId = reader.GetString(reader.GetOrdinal("VERTICAL_ID")),
                                    VerticalName = reader.GetString(reader.GetOrdinal("VERTICAL_NAME")),
                                    Status = reader.GetString(reader.GetOrdinal("STATUS"))
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetMappedVerticalsForRcmAsync for RCM SQ_NO {rcmSqNo}", rcmSqNo);
                throw;
            }
            return verticals;
        }
        public async Task RejectRcmPointAsync(int sqNo, string rejectionReason, string updatedBy)
        {
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.RCM_POINTS_PKG.reject_rcm_point", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("p_SQ_NO", OracleDbType.Int32, sqNo, ParameterDirection.Input);
                        command.Parameters.Add("p_REJECTION_REASON", OracleDbType.NVarchar2, rejectionReason, ParameterDirection.Input);
                        command.Parameters.Add("p_UPDATED_BY", OracleDbType.NVarchar2, updatedBy, ParameterDirection.Input);
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calling method RejectRcmPointAsync for SQ_NO {sqNo}", sqNo);
                throw;
            }
        }
        // NEW METHOD: Fetches a single file by its SQ_NO, including its binary data.
        public async Task<RcmPointFilesMaster?> GetFileByIdAsync(int fileSqNo)
        {
            RcmPointFilesMaster? file = null;
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.RCM_POINTS_PKG.get_file_by_id", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("p_FILE_SQ_NO", OracleDbType.Int32, fileSqNo, ParameterDirection.Input);
                        command.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                        await command.ExecuteNonQueryAsync();

                        using (var reader = ((OracleRefCursor)command.Parameters["p_RESULT_CURSOR"].Value).GetDataReader())
                        {
                            if (await reader.ReadAsync())
                            {
                                file = new RcmPointFilesMaster
                                {
                                    FileSqNo = reader.GetInt32(reader.GetOrdinal("FILE_SQ_NO")),
                                    FileName = reader.GetString(reader.GetOrdinal("FILE_NAME")),
                                    FileType = reader.GetString(reader.GetOrdinal("FILE_TYPE")),
                                    FileData = (byte[])reader["FILE_DATA"]
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching file with SQ_NO {FileSqNo}", fileSqNo);
                throw;
            }
            return file;
        }
        public async Task SubmitManagementReplyAsync(int sqNo, string managementReply, string updatedBy)
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand("IFCO.RCM_POINTS_PKG.submit_management_reply", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add("p_SQ_NO", OracleDbType.Int32, sqNo, ParameterDirection.Input);
                    command.Parameters.Add("p_MANAGEMENT_REPLY", OracleDbType.NVarchar2, managementReply, ParameterDirection.Input);
                    command.Parameters.Add("p_UPDATED_BY", OracleDbType.NVarchar2, updatedBy, ParameterDirection.Input);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task SubmitAdminReplyAsync(int sqNo, string adminReply, string updatedBy)
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new OracleCommand("IFCO.RCM_POINTS_PKG.submit_admin_reply", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add("p_SQ_NO", OracleDbType.Int32, sqNo, ParameterDirection.Input);
                    command.Parameters.Add("p_ADMIN_REPLY", OracleDbType.NVarchar2, adminReply, ParameterDirection.Input);
                    command.Parameters.Add("p_UPDATED_BY", OracleDbType.NVarchar2, updatedBy, ParameterDirection.Input);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task DeleteRcmPointAsync(int sqNo)
        {
            try
            {
                using (var connection = new OracleConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new OracleCommand("IFCO.RCM_POINTS_PKG.delete_rcm_point", connection))
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.Add("p_SQ_NO", OracleDbType.Int32, sqNo, ParameterDirection.Input);
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calling method DeleteRcmPointAsync for SQ_NO {sqNo}", sqNo);
                throw;
            }
        }
        #endregion
        #region Report Generation
        public async Task<List<VerticalDropdownItem>> GetActiveVerticalsAsync()
        {
            var list = new List<VerticalDropdownItem>();
            using (var conn = new OracleConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new OracleCommand("IFCO.REPORTS_PKG.get_active_verticals", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new VerticalDropdownItem
                            {
                                VerticalId = reader["VERTICAL_ID"].ToString(),
                                VerticalName = reader["VERTICAL_NAME"].ToString()
                            });
                        }
                    }
                }
            }
            return list;
        }

        // 2. Fetch Active RCMs
        public async Task<List<RcmDropdownItem>> GetActiveRcmsAsyncForReport()
        {
            var list = new List<RcmDropdownItem>();
            using (var conn = new OracleConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new OracleCommand("IFCO.REPORTS_PKG.get_active_rcms", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new RcmDropdownItem
                            {
                                RcmSqNo = reader["RCM_SQ_NO"].ToString(),
                                RcmName = reader["RCM_NAME"].ToString()
                            });
                        }
                    }
                }
            }
            return list;
        }

        // 3. Generate Report and Return Excel Byte Array
        public async Task<byte[]> GenerateReportExcelAsync(string reportType, string? verticalId, string? rcmId, DateTime? fromDate, DateTime? toDate)
        {
            using (var conn = new OracleConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new OracleCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Configure Command based on selection
                    if (reportType == "Vertical")
                    {
                        cmd.CommandText = "IFCO.REPORTS_PKG.get_report_by_vertical";
                        cmd.Parameters.Add("p_VERTICAL_ID", OracleDbType.Varchar2).Value = verticalId;
                    }
                    else if (reportType == "RCM")
                    {
                        // Using the correct SP name from your SQL Package
                        cmd.CommandText = "IFCO.REPORTS_PKG.get_report_by_rcm_list";
                        cmd.Parameters.Add("p_RCM_SQ_NO_LIST", OracleDbType.Varchar2).Value = rcmId;
                    }
                    else if (reportType == "Date")
                    {
                        cmd.CommandText = "IFCO.REPORTS_PKG.get_report_by_date_range";
                        cmd.Parameters.Add("p_FROM_DATE", OracleDbType.Date).Value = fromDate;
                        cmd.Parameters.Add("p_TO_DATE", OracleDbType.Date).Value = toDate;
                    }

                    // Output Cursor
                    cmd.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                    // Fill DataTable
                    DataTable dt = new DataTable("ReportData");
                    using (var adapter = new OracleDataAdapter(cmd))
                    {
                        adapter.Fill(dt);
                    }

                    // Convert DataTable to Excel (XLSX) using ClosedXML
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("Report");
                        worksheet.Cell(1, 1).InsertTable(dt); // Dump data
                        worksheet.Columns().AdjustToContents(); // Auto-fit columns

                        using (var stream = new MemoryStream())
                        {
                            workbook.SaveAs(stream);
                            return stream.ToArray();
                        }
                    }
                }
            }
        }
        // 4. Fetch Active RCMs by Vertical ID (For Vertical Users)
        public async Task<List<RcmDropdownItem>> GetActiveRcmsByVerticalAsync(string verticalId)
        {
            var list = new List<RcmDropdownItem>();
            using (var conn = new OracleConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new OracleCommand("IFCO.REPORTS_PKG.get_active_rcms_by_vertical", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    // Pass the Vertical ID as input
                    cmd.Parameters.Add("p_VERTICAL_ID", OracleDbType.Varchar2).Value = verticalId;

                    // Output Cursor
                    cmd.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new RcmDropdownItem
                            {
                                RcmSqNo = reader["RCM_SQ_NO"].ToString(),
                                RcmName = reader["RCM_NAME"].ToString()
                            });
                        }
                    }
                }
            }
            return list;
        }
        #endregion
        #region Quaterly Report Upload
        // 1. Get Reports Summary (List View)
        public async Task<(List<QuarterReportSummary> Reports, int TotalRecords)> GetQuarterReportsSummaryAsync(int pageNumber, int pageSize)
        {
            var list = new List<QuarterReportSummary>();
            int totalRecords = 0;

            using (var conn = new OracleConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new OracleCommand("IFCO.QUARTER_REPORTS_PKG.get_reports_summary", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Input Params
                    cmd.Parameters.Add("p_FINANCIAL_YEAR", OracleDbType.NVarchar2).Value = DBNull.Value; // Optional filter
                    cmd.Parameters.Add("p_PAGE_NUMBER", OracleDbType.Int32).Value = pageNumber;
                    cmd.Parameters.Add("p_PAGE_SIZE", OracleDbType.Int32).Value = pageSize;

                    // Output Params
                    var pTotal = cmd.Parameters.Add("p_TOTAL_RECORDS", OracleDbType.Int32, ParameterDirection.Output);
                    cmd.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new QuarterReportSummary
                            {
                                SqNo = Convert.ToInt32(reader["SQ_NO"]),
                                FinancialYear = reader["FINANCIAL_YEAR"].ToString(),
                                Q1File = reader["QUARTER_1_FILE"].ToString(),
                                Q2File = reader["QUARTER_2_FILE"].ToString(),
                                Q3File = reader["QUARTER_3_FILE"].ToString(),
                                Q4File = reader["QUARTER_4_FILE"].ToString()
                            });
                        }
                    }

                    // Read the output parameter value after execution
                    // Note: Oracle.ManagedDataAccess sometimes requires reading outputs after the reader is closed
                    if (pTotal.Value != null && pTotal.Value != DBNull.Value)
                    {
                        // Depending on ODP.NET version, sometimes this is OracleDecimal
                        totalRecords = Convert.ToInt32(pTotal.Value.ToString());
                    }
                }
            }
            return (list, totalRecords);
        }

        // 2. Upload Quarter Report
        public async Task UploadQuarterReportAsync(string finYear, int quarter, string fileName, byte[] fileData, string uploadedBy)
        {
            using (var conn = new OracleConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new OracleCommand("IFCO.QUARTER_REPORTS_PKG.upload_quarter_report", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("p_FINANCIAL_YEAR", OracleDbType.NVarchar2).Value = finYear;
                    cmd.Parameters.Add("p_QUARTER", OracleDbType.Int32).Value = quarter;
                    cmd.Parameters.Add("p_FILE_NAME", OracleDbType.Varchar2).Value = fileName;
                    cmd.Parameters.Add("p_FILE_DATA", OracleDbType.Blob).Value = fileData;
                    cmd.Parameters.Add("p_UPLOADED_BY", OracleDbType.Varchar2).Value = uploadedBy;

                    // Output param for Sequence Number (not strictly needed for UI but required by SP signature)
                    cmd.Parameters.Add("p_SQ_NO", OracleDbType.Int32, ParameterDirection.Output);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        // 3. Get Quarter File (Download)
        public async Task<(string FileName, byte[] FileData)> GetQuarterFileAsync(string finYear, int quarter)
        {
            using (var conn = new OracleConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new OracleCommand("IFCO.QUARTER_REPORTS_PKG.get_quarter_file", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("p_FINANCIAL_YEAR", OracleDbType.NVarchar2).Value = finYear;
                    cmd.Parameters.Add("p_QUARTER", OracleDbType.Int32).Value = quarter;
                    cmd.Parameters.Add("p_RESULT_CURSOR", OracleDbType.RefCursor, ParameterDirection.Output);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            string fileName = reader["FILE_NAME"].ToString();
                            byte[] fileData = (byte[])reader["FILE_DATA"];
                            return (fileName, fileData);
                        }
                    }
                }
            }
            throw new Exception("File not found.");
        }
        #endregion
        #region Session Management
        public async Task UpdateSessionTokenAsync(string userId, string userType, string sessionToken)
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Determine which package procedure to call
                string procedureName = userType == "Consultant"
                    ? "IFCO.CONSULTANT_MASTER_PKG.update_session_token"
                    : "IFCO.VERTICAL_USER_MASTER_PKG.update_session_token";

                // Determine the parameter name for the ID (p_CONSULTANT_ID or p_USER_ID)
                string idParamName = userType == "Consultant" ? "p_CONSULTANT_ID" : "p_USER_ID";

                using (var command = new OracleCommand(procedureName, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    // Bind parameters
                    command.Parameters.Add(idParamName, OracleDbType.NVarchar2, userId, ParameterDirection.Input);
                    command.Parameters.Add("p_SESSION_TOKEN", OracleDbType.NVarchar2, sessionToken, ParameterDirection.Input);

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<string?> GetSessionTokenAsync(string userId, string userType)
        {
            using (var connection = new OracleConnection(_connectionString))
            {
                await connection.OpenAsync();

                string procedureName = userType == "Consultant"
                    ? "IFCO.CONSULTANT_MASTER_PKG.get_session_token"
                    : "IFCO.VERTICAL_USER_MASTER_PKG.get_session_token";

                string idParamName = userType == "Consultant" ? "p_CONSULTANT_ID" : "p_USER_ID";

                using (var command = new OracleCommand(procedureName, connection))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.Add(idParamName, OracleDbType.NVarchar2, userId, ParameterDirection.Input);

                    // Output parameter for the token
                    var tokenParam = new OracleParameter("p_SESSION_TOKEN", OracleDbType.NVarchar2, 255); // Ensure size matches DB
                    tokenParam.Direction = ParameterDirection.Output;
                    command.Parameters.Add(tokenParam);

                    await command.ExecuteNonQueryAsync();

                    // Handle potential nulls safely
                    if (tokenParam.Value != null && tokenParam.Value != DBNull.Value)
                    {
                        // Accessing OracleString.Value or ToString() depending on the return type
                        return tokenParam.Value.ToString();
                    }
                    return null;
                }
            }
        }
        #endregion
    }
}