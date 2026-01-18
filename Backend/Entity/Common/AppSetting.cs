namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Common
{
    /// <summary>
    /// Clase para manejar las configuraciones de la aplicación
    /// </summary>
    public class AppSetting
    {
        public string ApplicationName { get; set; }
        public string ConsultaModeloRusell_BaseUrl { get; set; }
        public string ConsultaModeloRusell_TokenUrl { get; set; }
        public string ConsultaModeloRusell_RussellModelUrl { get; set; }
        public string ConsultaModeloRusell_API_Username { get; set; }
        public string ConsultaModeloRusell_API_Password { get; set; }
        public string ConsultaModeloRusell_API_GrantType { get; set; }
        public string ConsultaModeloRusell_AuxiliarModelUrl { get; set; }
        public string ConsultaModeloRusell_SpeechModelUrl { get; set; }
        public string ConsultaModeloRusell_ConvertionSpeechModelUrl { get; set; }
        public string PersonalInfo_Url { get; set; }
        public string PersonalInfo_GetPersonInfoUrl { get; set; }
        public string PersonalInfo_User { get; set; }
        public string PersonalInfo_Password { get; set; }
        public string PersonalInfo_TipoId { get; set; }
        public string PersonalInfo_TipoEstudio { get; set; }
        public int PersonalInfo_ValidationDays { get; set; }
        public string Becas_API_Uam { get; set; }
        public string Becas_API_Uam_ExistenciaEstudianteUrl { get; set; }
        public string Becas_API_Uam_SedeUrl { get; set; }
        public string Becas_API_Uam_PeriodoUrl { get; set; }
        public string Becas_API_Uam_FacultadUrl { get; set; }
        public string Becas_API_Uam_CarreraUrl { get; set; }
        public string Becas_API_Uam_EnfasisUrl { get; set; }
        public string Becas_API_Uam_GradoUrl { get; set; }
        public string Becas_API_Uam_PlanEstudiosUrl { get; set; }
        public string Becas_API_Uam_MateriasUrl { get; set; }
        public string Becas_API_Uam_GuardarBeca { get; set; }
        public string Becas_API_Uam_BeneficiosUrl { get; set; }
        public string Becas_API_Latina { get; set; }
        public string Becas_API_Latina_ExistenciaEstudianteUrl { get; set; }
        public string Becas_API_Latina_SedeUrl { get; set; }
        public string Becas_API_Latina_PeriodoUrl { get; set; }
        public string Becas_API_Latina_FacultadUrl { get; set; }
        public string Becas_API_Latina_CarreraUrl { get; set; }
        public string Becas_API_Latina_EnfasisUrl { get; set; }
        public string Becas_API_Latina_GradoUrl { get; set; }
        public string Becas_API_Latina_PlanEstudiosUrl { get; set; }
        public string Becas_API_Latina_MateriasUrl { get; set; }
        public string Becas_API_Latina_GuardarBeca { get; set; }
        public string Becas_API_Latina_BeneficiosUrl { get; set; }
        public string Becas_API_Username { get; set; }
        public string Becas_API_Password { get; set; }
        public string Becas_API_TokenUrl { get; set; }
        public string Becas_API_GrantType { get; set; }
        public string IntegracionesSalesforce_API_Latina { get; set; }
        public string IntegracionesSalesforce_API_Latina_PreMateriasUrl { get; set; }
        public string IntegracionesSalesforce_API_Latina_Username { get; set; }
        public string IntegracionesSalesforce_API_Latina_Password { get; set; }
        public string LatinaCode { get; set; }
        public string UamCode { get; set; }
        public string DateFormatDB { get; set; }
        public bool LogApiEnabled { get; set; }
    }
}
