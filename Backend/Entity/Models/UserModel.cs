namespace Academikus.AgenteInteligenteMentoresWebApi.Entity.Models;

public partial class UserModel
{
    public int Id { get; set; }

    // public string FavoriteName { get; set; }
    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string UserType { get; set; } = null!;

    public string UserState { get; set; } = null!;

    public virtual ICollection<ChatModel> MentorStudentMentors { get; set; } = new List<ChatModel>();

    public virtual ICollection<ChatModel> MentorStudentStudents { get; set; } = new List<ChatModel>();
}
