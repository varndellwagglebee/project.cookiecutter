﻿using FluentValidation;
using Lamar;

namespace {{ cookiecutter.assembly_name }}.Core.Validators;

public class ValidatorProvider : Validators.IValidatorProvider
{
    private readonly IContainer _container;

    public ValidatorProvider( IContainer container )
    {
        _container = container;
    }

    public IValidator<TPlugin> For<TPlugin>()
        where TPlugin : class
    {
        return _container.GetInstance<IValidator<TPlugin>>();
    }
}
