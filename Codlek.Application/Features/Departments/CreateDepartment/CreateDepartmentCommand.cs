using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Departments;
using MediatR;

namespace Codlek.Application.Features.Departments.CreateDepartment;

public sealed record CreateDepartmentCommand(string Name, string Code, int SortOrder)
    : IRequest<Result<DepartmentRow>>;
