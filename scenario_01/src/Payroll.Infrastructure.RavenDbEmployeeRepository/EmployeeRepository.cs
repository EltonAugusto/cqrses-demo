﻿using System;
using Payroll.Domain;
using Payroll.Domain.Model;
using Payroll.Domain.Repositories;
using Raven.Abstractions.Data;
using Raven.Abstractions.Extensions;
using Raven.Client;
using Raven.Client.Document;
using Raven.Imports.Newtonsoft.Json;
using Raven.Json.Linq;

namespace Payroll.Infrastructure.RavenDbEmployeeRepository
{
    public class EmployeeRepository : IEmployeeRepository, IDisposable
    {
        private readonly IDocumentStore _store;
        private JsonSerializer _serializer;

        public EmployeeRepository()
        {
            _store = new DocumentStore
            {
                Url = "http://localhost:8080/", // server URL
                DefaultDatabase = "RegularDb"
            };

            _store.Initialize();

            _serializer = _store.Conventions.CreateSerializer();
            _serializer.TypeNameHandling = TypeNameHandling.All;

            _store.Conventions.IdentityTypeConvertors.Add(
                new EmployeeIdConverter()
                );
        }

        
        public bool IsRegistered(EmployeeId id)
        {
            var lid = $"employees/{id}";
            return _store.DatabaseCommands.Head(lid) != null;
        }

        public Employee Load(EmployeeId id)
        {
            Employee result;
            using (var session = _store.OpenSession())
            {
                var lid = $"employees/{id}";
                result = session.Load<Employee>(lid);
            }
            return result;
        }

        public void CreateEmployee(EmployeeId id, FullName name, decimal initialSalary)
        {
            using (var session = _store.OpenSession())
            {
                var employee = new Employee(id, name, Address.NotInformed, initialSalary);
                session.Store(employee);
                session.SaveChanges();
            }
        }

        public void RaiseSalary(EmployeeId id, decimal amount)
        {
            _store.DatabaseCommands.Patch($"employees/{id}", new ScriptedPatchRequest
            {
                Script = $"this.Salary += {amount.ToInvariantString()};"
            });
        }

        public void UpdateHomeAddress(EmployeeId id, Address homeAddress)
        {
            var ro = RavenJObject.FromObject(homeAddress, _serializer);
            
            _store.DatabaseCommands.Patch($"employees/{id}", new[]
            {
                new PatchRequest
                {
                    Type = PatchCommandType.Set,
                    Name = "HomeAddress",
                    Value = ro
                }
            });
        }

        public void Dispose()
        {
            _store.Dispose();
        }
    }
}
