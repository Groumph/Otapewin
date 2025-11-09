# Contributing to SecondBrain

Thank you for your interest in contributing to SecondBrain! This document provides guidelines and instructions for contributing.

## Code of Conduct

By participating in this project, you are expected to uphold our Code of Conduct (see CODE_OF_CONDUCT.md).

## How Can I Contribute?

### Reporting Bugs

Before creating bug reports, please check the issue list as you might find out that you don't need to create one. When you are creating a bug report, please include as many details as possible:

* **Use a clear and descriptive title**
* **Describe the exact steps which reproduce the problem**
* **Provide specific examples to demonstrate the steps**
* **Describe the behavior you observed after following the steps**
* **Explain which behavior you expected to see instead and why**
* **Include screenshots if relevant**
* **Include your environment details** (OS, .NET version, etc.)

### Suggesting Enhancements

Enhancement suggestions are tracked as GitHub issues. When creating an enhancement suggestion, please include:

* **Use a clear and descriptive title**
* **Provide a step-by-step description of the suggested enhancement**
* **Provide specific examples to demonstrate the steps**
* **Describe the current behavior and explain which behavior you expected to see instead**
* **Explain why this enhancement would be useful**

### Pull Requests

* Fill in the required template
* Follow the C# coding style (enforced by .editorconfig)
* Include thoughtfully-worded, well-structured tests
* Document new code
* End all files with a newline
* Ensure all tests pass before submitting

## Development Setup

1. Fork and clone the repository
2. Install [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
3. Build the project: `dotnet build`
4. Run tests: `dotnet test`

## Coding Guidelines

### Style Guide

This project follows the standard .NET coding conventions. The `.editorconfig` file enforces these rules:

* Use 4 spaces for indentation
* Use PascalCase for class names and method names
* Use camelCase for local variables and parameters
* Prefix interfaces with `I`
* Use meaningful names
* Keep methods focused and concise
* Add XML documentation comments for public APIs

### Git Commit Messages

* Use the present tense ("Add feature" not "Added feature")
* Use the imperative mood ("Move cursor to..." not "Moves cursor to...")
* Limit the first line to 72 characters or less
* Reference issues and pull requests liberally after the first line

### Branch Naming

* Feature branches: `feature/description`
* Bug fixes: `fix/description`
* Documentation: `docs/description`

## Testing

* Write unit tests for new features
* Ensure all existing tests pass
* Aim for high code coverage
* Use meaningful test names that describe what is being tested

## Documentation

* Update README.md if you change functionality
* Add XML documentation comments for public APIs
* Update relevant documentation for any changed behavior

## Questions?

Feel free to open an issue with your question or reach out to the maintainers.

Thank you for contributing! 🎉
