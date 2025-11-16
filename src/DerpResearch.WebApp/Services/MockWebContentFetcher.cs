using DeepResearch.WebApp.Interfaces;

namespace DeepResearch.WebApp.Services;

/// <summary>
/// Mock implementation of IWebContentFetcher for testing UX without actual web scraping
/// </summary>
public class MockWebContentFetcher : IWebContentFetcher
{
    private readonly ILogger<MockWebContentFetcher> _logger;
    private static readonly Random _random = new();

    public MockWebContentFetcher(ILogger<MockWebContentFetcher> logger)
    {
        _logger = logger;
        _logger.LogInformation("MockWebContentFetcher initialized - will return simulated content");
    }

    public async Task<Dictionary<string, string>> FetchContentAsync(string[] urls, int timeoutSeconds = 5, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation("Mock fetching content for {Count} URLs", urls.Length);

        var results = new Dictionary<string, string>();

        foreach (var url in urls)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // Simulate network delay
            await Task.Delay(_random.Next(100, 500), cancellationToken);

            // Generate realistic mock content based on URL
            var content = GenerateMockContent(url);
            results[url] = content;

            _logger.LogInformation("Mock fetched {Length} characters from {Url}", 
                content.Length, url);
        }

        return results;
    }

    private string GenerateMockContent(string url)
    {
        // Extract query or topic from URL to make content somewhat relevant
        var topic = ExtractTopicFromUrl(url);

        var templates = new[]
        {
            GenerateArticleTemplate(topic),
            GenerateTechnicalDocumentation(topic),
            GenerateBlogPost(topic),
            GenerateResearchPaper(topic),
            GenerateTutorial(topic)
        };

        return templates[_random.Next(templates.Length)];
    }

    private string ExtractTopicFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var query = uri.Query;
            
            if (query.Contains("q="))
            {
                var start = query.IndexOf("q=") + 2;
                var end = query.IndexOf('&', start);
                var topic = end > start 
                    ? query.Substring(start, end - start) 
                    : query.Substring(start);
                return Uri.UnescapeDataString(topic);
            }

            // Fallback to path-based topic extraction
            var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return pathSegments.Length > 0 ? pathSegments[^1].Replace('-', ' ') : "the topic";
        }
        catch
        {
            return "the topic";
        }
    }

    private string GenerateArticleTemplate(string topic)
    {
        return $@"# Understanding {topic}: A Comprehensive Guide

## Introduction

{topic} has become increasingly important in modern applications and systems. This article explores the fundamental concepts, practical applications, and best practices associated with {topic}.

## What is {topic}?

{topic} represents a sophisticated approach to solving complex technical challenges. It combines proven methodologies with innovative techniques to deliver reliable and scalable solutions.

### Key Characteristics

1. **Efficiency**: Optimized for performance and resource utilization
2. **Scalability**: Designed to handle growing demands
3. **Reliability**: Built with robust error handling and fault tolerance
4. **Maintainability**: Clear architecture and well-documented patterns

## Technical Architecture

The underlying architecture of {topic} consists of several interconnected components working together seamlessly. The design emphasizes modularity and separation of concerns, making it easier to understand, test, and extend.

### Core Components

- **Processing Layer**: Handles data transformation and business logic
- **Storage Layer**: Manages persistent data with optimized access patterns
- **Interface Layer**: Provides APIs and user interfaces
- **Integration Layer**: Connects with external systems and services

## Practical Applications

Organizations across various industries have successfully implemented {topic} to solve real-world problems. Common use cases include:

1. Large-scale data processing and analytics
2. Real-time system integration
3. Distributed computing environments
4. High-availability service architectures

## Implementation Considerations

When implementing {topic}, several factors need careful consideration:

### Performance Optimization

Performance tuning involves analyzing bottlenecks, optimizing algorithms, and leveraging caching strategies. Monitoring and profiling tools help identify areas for improvement.

### Security Best Practices

Security should be built into every layer of the system. This includes input validation, authentication, authorization, encryption, and regular security audits.

### Scalability Planning

Designing for scale from the beginning prevents costly rewrites later. Consider horizontal scaling, load balancing, and distributed architectures.

## Comparison with Alternatives

While {topic} offers many advantages, it's important to understand how it compares to alternative approaches:

- **Traditional Methods**: More established but may lack modern features
- **Competing Solutions**: Different trade-offs in complexity vs. capability
- **Hybrid Approaches**: Combining multiple strategies for optimal results

## Future Trends

The field continues to evolve with emerging technologies and methodologies. Recent developments suggest increased automation, improved tooling, and better integration capabilities.

## Conclusion

{topic} provides a powerful framework for building modern applications. By understanding its principles and following best practices, teams can create robust, scalable, and maintainable systems.

## References

- Industry standards and specifications
- Academic research papers
- Case studies and success stories
- Community documentation and guides";
    }

    private string GenerateTechnicalDocumentation(string topic)
    {
        return $@"# {topic} - Technical Documentation

## Overview

This document provides technical specifications and implementation details for {topic}.

## System Requirements

### Hardware Requirements
- CPU: Multi-core processor (4+ cores recommended)
- RAM: 8GB minimum, 16GB recommended
- Storage: SSD with sufficient capacity
- Network: Stable internet connection

### Software Requirements
- Operating System: Windows 10/11, Linux (Ubuntu 20.04+), or macOS 11+
- Runtime Environment: .NET 9.0 or equivalent
- Database: SQLite, PostgreSQL, or compatible
- Additional Tools: Git, Docker (optional)

## Architecture Design

The system follows a layered architecture pattern with clear separation of concerns:

```
┌─────────────────────────────┐
│     Presentation Layer      │
├─────────────────────────────┤
│      Business Logic         │
├─────────────────────────────┤
│      Data Access Layer      │
├─────────────────────────────┤
│     Infrastructure          │
└─────────────────────────────┘
```

## API Reference

### Core Interfaces

The system exposes several key interfaces for integration:

1. **Data Processing Interface**: Handles input/output operations
2. **Configuration Interface**: Manages system settings
3. **Monitoring Interface**: Provides health checks and metrics
4. **Integration Interface**: Connects with external systems

## Configuration Options

Configuration is managed through environment variables and configuration files:

- `SYSTEM_MODE`: Development, Staging, or Production
- `MAX_CONNECTIONS`: Maximum concurrent connections (default: 100)
- `TIMEOUT_SECONDS`: Request timeout in seconds (default: 30)
- `LOG_LEVEL`: Logging verbosity (Debug, Info, Warning, Error)

## Performance Characteristics

### Benchmarks

Under typical load conditions:
- Response Time: < 100ms (p95)
- Throughput: 1000+ requests/second
- Availability: 99.9% uptime
- Resource Usage: < 2GB RAM under normal load

## Security Considerations

### Authentication
Supports multiple authentication mechanisms including API keys, OAuth 2.0, and JWT tokens.

### Authorization
Role-based access control (RBAC) with fine-grained permissions.

### Data Protection
All data encrypted at rest and in transit using industry-standard protocols.

## Troubleshooting

### Common Issues

**Issue**: Connection timeout errors
**Solution**: Check network connectivity and firewall settings

**Issue**: High memory usage
**Solution**: Review caching configuration and adjust limits

**Issue**: Slow query performance
**Solution**: Add appropriate indexes and optimize queries

## Deployment

Supports multiple deployment strategies:
- Traditional server deployment
- Containerized deployment (Docker)
- Cloud platform deployment (Azure, AWS, GCP)
- Kubernetes orchestration

## Monitoring and Observability

Integrated monitoring provides:
- Real-time performance metrics
- Error tracking and alerting
- Distributed tracing
- Log aggregation and analysis";
    }

    private string GenerateBlogPost(string topic)
    {
        return $@"# Exploring {topic}: A Deep Dive

Posted on {DateTime.UtcNow:MMMM dd, yyyy}

Have you ever wondered about {topic}? Today, we're going to explore this fascinating subject in detail and uncover what makes it so important in modern software development.

## Why {topic} Matters

In today's fast-paced technology landscape, {topic} has emerged as a critical component of successful systems. Organizations that leverage {topic} effectively gain significant competitive advantages.

## Getting Started with {topic}

If you're new to {topic}, don't worry! The concepts are easier to grasp than they might initially appear. Let's break down the fundamentals:

### The Basics

At its core, {topic} is about solving specific problems efficiently. The key principles include:

- Understanding the problem domain
- Choosing appropriate tools and techniques
- Following established best practices
- Iterating based on feedback

## Real-World Examples

Let me share some real-world examples of {topic} in action:

**Example 1: E-commerce Platform**
A major e-commerce company implemented {topic} to handle millions of daily transactions. The results were impressive: 40% improvement in response time and 60% reduction in resource costs.

**Example 2: Healthcare System**
A healthcare provider used {topic} to integrate disparate systems. This improved patient care coordination and reduced administrative overhead by 30%.

**Example 3: Financial Services**
A fintech startup built their entire platform on {topic} principles, enabling them to scale from zero to millions of users in just 18 months.

## Lessons Learned

Through these implementations, several key lessons emerged:

1. **Start Simple**: Begin with a minimal viable implementation
2. **Measure Everything**: Track metrics to understand impact
3. **Iterate Quickly**: Don't aim for perfection on the first try
4. **Learn from Others**: Study existing implementations and case studies

## Common Pitfalls to Avoid

Based on community experience, here are common mistakes to avoid:

- Over-engineering the initial solution
- Neglecting proper testing and validation
- Ignoring security considerations
- Failing to plan for scale

## Tools and Resources

To help you on your {topic} journey, here are some valuable resources:

- Official documentation and guides
- Community forums and discussion boards
- Open-source projects and examples
- Training courses and certifications

## Looking Ahead

The future of {topic} looks bright. Emerging trends include increased automation, better tooling, and deeper integration with AI/ML technologies.

## Conclusion

{topic} represents an exciting opportunity for developers and organizations alike. By understanding its principles and applying them thoughtfully, you can build better, more efficient systems.

What has your experience with {topic} been? Share your thoughts in the comments below!

---

*Tags: {topic}, software development, best practices, architecture*";
    }

    private string GenerateResearchPaper(string topic)
    {
        return $@"# {topic}: A Systematic Review and Analysis

## Abstract

This paper presents a comprehensive analysis of {topic}, examining its theoretical foundations, practical applications, and future directions. Through systematic review of existing literature and empirical evaluation, we identify key trends, challenges, and opportunities in this domain.

**Keywords**: {topic}, software engineering, system design, performance optimization

## 1. Introduction

{topic} has gained significant attention in recent years due to its potential to address complex computational challenges. This paper provides a systematic review of the field, analyzing both theoretical contributions and practical implementations.

### 1.1 Background

The foundations of {topic} can be traced back to early work in distributed systems and software architecture. Over the past decade, advances in cloud computing and containerization have accelerated adoption.

### 1.2 Research Questions

This study addresses the following research questions:

1. What are the fundamental principles underlying {topic}?
2. How does {topic} compare to alternative approaches?
3. What are the key challenges in implementing {topic}?
4. What future research directions show the most promise?

## 2. Methodology

Our research methodology consisted of:

- Systematic literature review of 150+ papers
- Case study analysis of 20 real-world implementations
- Performance benchmarking across multiple scenarios
- Expert interviews with industry practitioners

## 3. Theoretical Framework

### 3.1 Core Concepts

{topic} is built upon several foundational concepts:

- **Modularity**: Separation of concerns and component isolation
- **Abstraction**: Hiding complexity behind well-defined interfaces
- **Composition**: Building complex systems from simple components
- **Scalability**: Handling growth in load and complexity

### 3.2 Mathematical Formalization

The performance characteristics of {topic} can be modeled using:

P(n) = O(n log n)

where n represents the input size and P represents processing time.

## 4. Empirical Analysis

### 4.1 Performance Evaluation

We conducted extensive performance testing across various scenarios:

- **Throughput**: Average 1,500 operations/second
- **Latency**: P95 latency of 85ms
- **Resource Utilization**: 70% CPU, 60% memory under load
- **Scalability**: Linear scaling up to 10,000 concurrent users

### 4.2 Comparative Analysis

Comparison with alternative approaches reveals:

| Metric | {topic} | Alternative A | Alternative B |
|--------|---------|--------------|--------------|
| Performance | High | Medium | Medium |
| Complexity | Medium | Low | High |
| Flexibility | High | Medium | High |
| Maturity | High | High | Medium |

## 5. Case Studies

### 5.1 Enterprise Implementation

A Fortune 500 company deployed {topic} to modernize their legacy systems. Results included 50% reduction in maintenance costs and 3x improvement in deployment frequency.

### 5.2 Startup Adoption

An early-stage startup built their entire platform using {topic} principles, enabling rapid iteration and scaling from prototype to production in 6 months.

## 6. Challenges and Limitations

Despite its advantages, {topic} faces several challenges:

- **Learning Curve**: Requires significant expertise to implement effectively
- **Tooling Gaps**: Ecosystem still maturing in some areas
- **Migration Complexity**: Transitioning from legacy systems can be challenging
- **Cost Considerations**: Initial implementation may require substantial investment

## 7. Future Directions

Promising areas for future research include:

- Integration with emerging technologies (AI/ML, edge computing)
- Improved automation and tooling
- Enhanced security and privacy mechanisms
- Better support for hybrid architectures

## 8. Conclusion

{topic} represents a significant advancement in software engineering practice. Our analysis demonstrates its effectiveness across diverse use cases while highlighting areas for continued research and development.

## References

[1] Smith, J. et al. (2023). Foundations of {topic}. Journal of Software Engineering.
[2] Johnson, A. (2023). Practical Guide to {topic}. Tech Press.
[3] Williams, R. et al. (2024). Performance Analysis of {topic} Systems. ACM SIGSOFT.
[4] Chen, L. (2024). {topic} in Production Environments. IEEE Software.";
    }

    private string GenerateTutorial(string topic)
    {
        return $@"# Step-by-Step Tutorial: Mastering {topic}

Welcome to this comprehensive tutorial on {topic}! By the end of this guide, you'll have a solid understanding of how to implement and use {topic} effectively.

## What You'll Learn

- Core concepts and terminology
- Setting up your development environment
- Building your first {topic} application
- Best practices and common patterns
- Troubleshooting and debugging techniques

## Prerequisites

Before starting, make sure you have:
- Basic programming knowledge
- Development environment set up
- Text editor or IDE installed
- Internet connection for downloading dependencies

## Part 1: Understanding the Basics

Let's start with the fundamentals. {topic} is designed to help developers solve specific problems more efficiently.

### Key Concepts

**Concept 1: Foundation**
The foundation layer provides core functionality that everything else builds upon.

**Concept 2: Composition**
You combine simple building blocks to create complex functionality.

**Concept 3: Configuration**
Flexible configuration allows customization for different use cases.

## Part 2: Setting Up

### Step 1: Installation

First, install the necessary tools and dependencies:

```bash
# Install core package
npm install {topic}

# Install additional tools
npm install {topic}-cli --global
```

### Step 2: Initial Configuration

Create a configuration file:

```json
{{
  ""version"": ""1.0"",
  ""settings"": {{
    ""mode"": ""development"",
    ""verbose"": true
  }}
}}
```

### Step 3: Verify Installation

Check that everything is working:

```bash
{topic} --version
```

## Part 3: Building Your First Application

Let's build a simple application to demonstrate key concepts.

### Step 4: Project Structure

Create the following directory structure:

```
my-project/
  ├── src/
  ├── config/
  ├── tests/
  └── README.md
```

### Step 5: Core Implementation

Implement the main logic:

```csharp
public class Example
{{
    public void ProcessData()
    {{
        // Implementation here
        Console.WriteLine(""Processing with {topic}"");
    }}
}}
```

### Step 6: Running the Application

Execute your application:

```bash
dotnet run
```

## Part 4: Advanced Techniques

### Optimization Strategies

1. **Caching**: Implement caching for frequently accessed data
2. **Parallelization**: Use async/await for concurrent operations
3. **Resource Pooling**: Reuse expensive resources

### Error Handling

Implement robust error handling:

```csharp
try
{{
    ProcessData();
}}
catch (Exception ex)
{{
    Logger.LogError(ex, ""Processing failed"");
    // Handle error appropriately
}}
```

## Part 5: Best Practices

### Code Organization

- Keep functions small and focused
- Use meaningful names
- Write comprehensive tests
- Document public interfaces

### Performance Tips

- Profile before optimizing
- Measure actual impact
- Consider trade-offs
- Monitor in production

## Part 6: Common Pitfalls

**Pitfall 1: Over-Complication**
Keep it simple initially. Add complexity only when needed.

**Pitfall 2: Ignoring Edge Cases**
Test thoroughly with various inputs and scenarios.

**Pitfall 3: Poor Error Handling**
Always handle errors gracefully and provide useful messages.

## Part 7: Next Steps

Now that you understand the basics, explore:

- Advanced patterns and techniques
- Integration with other systems
- Production deployment strategies
- Community resources and support

## Troubleshooting

### Common Issues

**Issue**: Application won't start
**Solution**: Check configuration files and dependencies

**Issue**: Poor performance
**Solution**: Review logging output and use profiling tools

**Issue**: Unexpected behavior
**Solution**: Enable debug mode and examine detailed logs

## Conclusion

Congratulations! You've completed the {topic} tutorial. You now have the knowledge to build your own applications and continue learning.

## Additional Resources

- Official documentation
- Community forum
- GitHub repository
- Video tutorials
- Sample projects

Happy coding!";
    }
}
