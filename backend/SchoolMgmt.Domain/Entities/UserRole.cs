namespace SchoolMgmt.Domain.Entities;

public enum UserRole
{
    Admin,
    Teacher,
    Principal, // canonical name for "Principal/Owner" — one role string, not two
    Parent,
}
