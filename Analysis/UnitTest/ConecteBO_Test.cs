//using Microsoft.Extensions.Logging;
//using Microsoft.Extensions.Options;
//using Microsoft.VisualStudio.TestTools.UnitTesting;
//using Moq;
//using Academikus.AnalysisMentoresVerdes.Business.Conecte;
//using Academikus.AnalysisMentoresVerdes.Data.DB.Command.Core;
//using Academikus.AnalysisMentoresVerdes.Data.DB.Command.Scholarship;
//using Academikus.AnalysisMentoresVerdes.Entity.Common;
//using Academikus.AnalysisMentoresVerdes.Utility.WebApi;
//using Assert = NUnit.Framework.Assert;

//namespace Academikus.AnalysisMentoresVerdes.Test
//{
//    /// <summary>
//    /// Clase de pruebas de ConecteBO
//    /// </summary>
//    public class Tests
//    {
//        [SetUp]
//        public void Setup()
//        {
//        }

//        [Test(Author = "diego.jimenez.bautista@udla.edu.ec")]
//        public void GetConnectData_ConnectDataExists_ReturnJsonString()
//        {
//            // Arrange
//            AppSetting _appSetting = new AppSetting()
//            {
//                PersonalInfo_Url = "https://api.conecte.cr/",
//                PersonalInfo_GetPersonInfoUrl = "api/GenerarEstudio",
//                PersonalInfo_User = "WS_ULATINA",
//                PersonalInfo_Password = "BAFC4B03A1",
//                PersonalInfo_TipoId = "1",
//                PersonalInfo_TipoEstudio = "2",
//                DateFormatDB = "yyyy-MM-dd HH:mm:ss"
//            };
//            var mockAppSetting = new Mock<IOptions<AppSetting>>();
//            mockAppSetting.Setup(ap => ap.Value).Returns(_appSetting);

//            var mock = new Mock<ILogger<ConecteBO>>();
//            ILogger<ConecteBO> mockLogger = mock.Object;

//            var mockScholarshipRepository = new Mock<IScholarshipRepository>(MockBehavior.Strict);

//            string identificacion = "119530096";

//            var mockWeb = new Mock<ILogger<WebApiInvokerConecte>>();
//            ILogger<WebApiInvokerConecte> mockWebApiInvokerConecte = mockWeb.Object;

//            var _webApiInvokerConecte = new WebApiInvokerConecte(mockWebApiInvokerConecte);

//            var conecteBO = new ConecteBO(mockAppSetting.Object, mockScholarshipRepository.Object, _webApiInvokerConecte, mockLogger);

//            mockLogger.LogInformation("GetConnectData");

//            // Act
//            var connectData = conecteBO.GetConnectData(identificacion).Result;

//            // Assert
//            Assert.That(connectData, Is.Null);
//        }

//        [Test(Author = "diego.jimenez.bautista@udla.edu.ec")]
//        [ExpectedException(typeof(Exception), "La api no devolvió un código exitoso")]
//        public void GetConnectData_ConnectDataExists_ReturnException()
//        {
//            // Arrange
//            AppSetting _appSetting = new AppSetting()
//            {
//                PersonalInfo_Url = "https://api.conecte.cr/",
//                PersonalInfo_GetPersonInfoUrl = "api/GenerarEstudio1",
//                PersonalInfo_User = "WS_ULATINA",
//                PersonalInfo_Password = "BAFC4B03A1*",
//                PersonalInfo_TipoId = "1",
//                PersonalInfo_TipoEstudio = "2",
//                DateFormatDB = "yyyy-MM-dd HH:mm:ss"
//            };
//            var mockAppSetting = new Mock<IOptions<AppSetting>>();
//            mockAppSetting.Setup(ap => ap.Value).Returns(_appSetting);

//            var mock = new Mock<ILogger<ConecteBO>>();
//            ILogger<ConecteBO> mockLogger = mock.Object;

//            var mockScholarshipRepository = new Mock<IScholarshipRepository>(MockBehavior.Strict);

//            string identificacion = "119530096965";

//            var mockWebApiInvoker = new Mock<ILogger<WebApiInvoker>>();
//            ILogger<WebApiInvoker> mockLoggerWebApiInvoker = mockWebApiInvoker.Object;

//            var _webApiInvokerConecte = new WebApiInvokerConecte(mockLoggerWebApiInvoker);

//            var conecteBO = new ConecteBO(mockAppSetting.Object, mockScholarshipRepository.Object, _webApiInvokerConecte, mockLogger);

//            // Act

//            // Assert
//            Assert.That(() => conecteBO.GetConnectData(identificacion), Throws.Exception);
//        }

//        [Test(Author = "diego.jimenez.bautista@udla.edu.ec")]
//        public void GetConnectObjectData_ConnectDataDBExists_ReturnJsonObject()
//        {
//            // Arrange
//            AppSetting _appSetting = new AppSetting()
//            {
//                PersonalInfo_Url = "https://api.conecte.cr/",
//                PersonalInfo_GetPersonInfoUrl = "api/GenerarEstudio",
//                PersonalInfo_User = "WS_ULATINA",
//                PersonalInfo_Password = "BAFC4B03A1",
//                PersonalInfo_TipoId = "1",
//                PersonalInfo_TipoEstudio = "2",
//                DateFormatDB = "yyyy-MM-dd HH:mm:ss",
//                PersonalInfo_ValidationDays = 30
//            };
//            var mockAppSetting = new Mock<IOptions<AppSetting>>();
//            mockAppSetting.Setup(ap => ap.Value).Returns(_appSetting);

//            ///BD
//            ConnectionString _connectionString = new ConnectionString()
//            {
//                ScholarshipRequestConnection = "Server=.;Database=ScholarshipRequestDB;Trusted_Connection=True;TrustServerCertificate=True;Pooling=False;MultipleActiveResultSets=True"
//            };

//            var mockConnectionString = new Mock<IOptions<ConnectionString>>();
//            mockConnectionString.Setup(ap => ap.Value).Returns(_connectionString);

//            var mock = new Mock<ILogger<ConecteBO>>();
//            ILogger<ConecteBO> mockLogger = mock.Object;

//            var mockScholarshipRepository = new Mock<ILogger<ScholarshipRepository>>();
//            ILogger<ScholarshipRepository> mockLoggerScholarshipRepository = mockScholarshipRepository.Object;

//            var mockCommandExecutor = new Mock<ICommandExecutor>();
//            var _mockCommandExecutor = new CommandExecutor();

//            string brandCode = "ULATINA";
//            string identificacion = "119530096";

//            var mockWeb = new Mock<ILogger<WebApiInvokerConecte>>();
//            ILogger<WebApiInvokerConecte> mockWebApiInvokerConecte = mockWeb.Object;

//            var _webApiInvokerConecte = new WebApiInvokerConecte(mockWebApiInvokerConecte);

//            var _scholarshipRepository = new ScholarshipRepository(mockAppSetting.Object, mockConnectionString.Object, _mockCommandExecutor, mockScholarshipRepository.Object);

//            var conecteBO = new ConecteBO(mockAppSetting.Object, _scholarshipRepository, _webApiInvokerConecte, mockLogger);

//            mockLogger.LogInformation("GetConnectData");

//            // Act
//            var connectData = conecteBO.GetConnectObjectData(brandCode, identificacion);

//            // Assert
//            Assert.That(connectData, Is.Null);
//        }

//        [Test(Author = "diego.jimenez.bautista@udla.edu.ec")]
//        public void GetConnectObjectData_ConnectDataDBNotExists_ReturnJsonObject()
//        {
//            // Arrange
//            AppSetting _appSetting = new AppSetting()
//            {
//                PersonalInfo_Url = "https://api.conecte.cr/",
//                PersonalInfo_GetPersonInfoUrl = "api/GenerarEstudio",
//                PersonalInfo_User = "WS_ULATINA",
//                PersonalInfo_Password = "BAFC4B03A1",
//                PersonalInfo_TipoId = "1",
//                PersonalInfo_TipoEstudio = "2",
//                DateFormatDB = "yyyy-MM-dd HH:mm:ss",
//                PersonalInfo_ValidationDays = 30
//            };
//            var mockAppSetting = new Mock<IOptions<AppSetting>>();
//            mockAppSetting.Setup(ap => ap.Value).Returns(_appSetting);

//            ///
//            ConnectionString _connectionString = new ConnectionString()
//            {
//                ScholarshipRequestConnection = "Server=.;Database=ScholarshipRequestDB;Trusted_Connection=True;TrustServerCertificate=True;Pooling=False;MultipleActiveResultSets=True"
//            };
//            var mockConnectionString = new Mock<IOptions<ConnectionString>>();
//            mockConnectionString.Setup(ap => ap.Value).Returns(_connectionString);

//            var mock = new Mock<ILogger<ConecteBO>>();
//            ILogger<ConecteBO> mockLogger = mock.Object;

//            var mockScholarshipRepository = new Mock<ILogger<ScholarshipRepository>>();
//            ILogger<ScholarshipRepository> mockLoggerScholarshipRepository = mockScholarshipRepository.Object;

//            var mockCommandExecutor = new Mock<ICommandExecutor>();
//            var _mockCommandExecutor = new CommandExecutor();

//            string brandCode = "ULATINA";
//            string identificacion = "702260686";

//            var mockWeb = new Mock<ILogger<WebApiInvokerConecte>>();
//            ILogger<WebApiInvokerConecte> mockWebApiInvokerConecte = mockWeb.Object;

//            var _webApiInvokerConecte = new WebApiInvokerConecte(mockWebApiInvokerConecte);

//            var _scholarshipRepository = new ScholarshipRepository(mockAppSetting.Object, mockConnectionString.Object, _mockCommandExecutor, mockScholarshipRepository.Object);

//            var conecteBO = new ConecteBO(mockAppSetting.Object, _scholarshipRepository, _webApiInvokerConecte, mockLogger);

//            mockLogger.LogInformation("GetConnectData");

//            // Act
//            var connectData = conecteBO.GetConnectObjectData(brandCode, identificacion);

//            // Assert
//            Assert.That(connectData, Is.Null);
//        }

//        [Test(Author = "diego.jimenez.bautista@udla.edu.ec")]
//        [ExpectedException(typeof(Exception), "No existe información en conecte.")]
//        public void GetConnectObjectData_ConnectDataApiNotExists_ReturnException()
//        {
//            // Arrange
//            AppSetting _appSetting = new AppSetting()
//            {
//                PersonalInfo_Url = "https://api.conecte.cr/",
//                PersonalInfo_GetPersonInfoUrl = "api/GenerarEstudio",
//                PersonalInfo_User = "WS_ULATINA",
//                PersonalInfo_Password = "BAFC4B03A1",
//                PersonalInfo_TipoId = "1",
//                PersonalInfo_TipoEstudio = "2",
//                DateFormatDB = "yyyy-MM-dd HH:mm:ss",
//                PersonalInfo_ValidationDays = 30
//            };
//            var mockAppSetting = new Mock<IOptions<AppSetting>>();
//            mockAppSetting.Setup(ap => ap.Value).Returns(_appSetting);

//            ///
//            ConnectionString _connectionString = new ConnectionString()
//            {
//                ScholarshipRequestConnection = "Server=.;Database=ScholarshipRequestDB;Trusted_Connection=True;TrustServerCertificate=True;Pooling=False;MultipleActiveResultSets=True"
//            };
//            var mockConnectionString = new Mock<IOptions<ConnectionString>>();
//            mockConnectionString.Setup(ap => ap.Value).Returns(_connectionString);

//            var mock = new Mock<ILogger<ConecteBO>>();
//            ILogger<ConecteBO> mockLogger = mock.Object;

//            var mockScholarshipRepository = new Mock<ILogger<ScholarshipRepository>>();
//            ILogger<ScholarshipRepository> mockLoggerScholarshipRepository = mockScholarshipRepository.Object;

//            var mockCommandExecutor = new Mock<ICommandExecutor>();
//            var _mockCommandExecutor = new CommandExecutor();

//            string brandCode = "ULATINA";
//            string identificacion = "70226068652";

//            var mockWeb = new Mock<ILogger<WebApiInvokerConecte>>();
//            ILogger<WebApiInvokerConecte> mockWebApiInvokerConecte = mockWeb.Object;

//            var _webApiInvokerConecte = new WebApiInvokerConecte(mockWebApiInvokerConecte);

//            var _scholarshipRepository = new ScholarshipRepository(mockAppSetting.Object, mockConnectionString.Object, _mockCommandExecutor, mockScholarshipRepository.Object);

//            var conecteBO = new ConecteBO(mockAppSetting.Object, _scholarshipRepository, _webApiInvokerConecte, mockLogger);

//            mockLogger.LogInformation("GetConnectData");

//            // Act

//            // Assert
//            Assert.That(() => conecteBO.GetConnectObjectData(brandCode, identificacion), Throws.Exception);
//        }

//        [Test(Author = "diego.jimenez.bautista@udla.edu.ec")]
//        [ExpectedException(typeof(Exception), "La api no devolvió un código exitoso")]
//        public void GetConnectObjectData_ConnectDataApiReturnErrorHttp_ReturnException()
//        {
//            // Arrange
//            AppSetting _appSetting = new AppSetting()
//            {
//                PersonalInfo_Url = "https://api.conecte.cr/",
//                PersonalInfo_GetPersonInfoUrl = "api/GenerarEstudio1",
//                PersonalInfo_User = "WS_ULATINA",
//                PersonalInfo_Password = "BAFC4B03A1",
//                PersonalInfo_TipoId = "1",
//                PersonalInfo_TipoEstudio = "2",
//                DateFormatDB = "yyyy-MM-dd HH:mm:ss",
//                PersonalInfo_ValidationDays = 30
//            };
//            var mockAppSetting = new Mock<IOptions<AppSetting>>();
//            mockAppSetting.Setup(ap => ap.Value).Returns(_appSetting);

//            ///
//            ConnectionString _connectionString = new ConnectionString()
//            {
//                ScholarshipRequestConnection = "Server=.;Database=ScholarshipRequestDB;Trusted_Connection=True;TrustServerCertificate=True;Pooling=False;MultipleActiveResultSets=True"
//            };
//            var mockConnectionString = new Mock<IOptions<ConnectionString>>();
//            mockConnectionString.Setup(ap => ap.Value).Returns(_connectionString);

//            var mock = new Mock<ILogger<ConecteBO>>();
//            ILogger<ConecteBO> mockLogger = mock.Object;

//            var mockScholarshipRepository = new Mock<ILogger<ScholarshipRepository>>();
//            ILogger<ScholarshipRepository> mockLoggerScholarshipRepository = mockScholarshipRepository.Object;

//            var mockCommandExecutor = new Mock<ICommandExecutor>();
//            var _mockCommandExecutor = new CommandExecutor();

//            string brandCode = "ULATINA";
//            string identificacion = "70226068652";

//            var mockWeb = new Mock<ILogger<WebApiInvokerConecte>>();
//            ILogger<WebApiInvokerConecte> mockWebApiInvokerConecte = mockWeb.Object;

//            var _webApiInvokerConecte = new WebApiInvokerConecte(mockWebApiInvokerConecte);

//            var _scholarshipRepository = new ScholarshipRepository(mockAppSetting.Object, mockConnectionString.Object, _mockCommandExecutor, mockScholarshipRepository.Object);

//            var conecteBO = new ConecteBO(mockAppSetting.Object, _scholarshipRepository, _webApiInvokerConecte, mockLogger);

//            mockLogger.LogInformation("GetConnectData");

//            // Act

//            // Assert
//            Assert.That(() => conecteBO.GetConnectObjectData(brandCode, identificacion), Throws.Exception);
//        }
//    }
//}