using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Docker.DotNet;
using Docker.DotNet.Models;

namespace DockerUpgradeService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly DockerClient _client;
        private DockerContainer runningContainer;
        private const string _defaultContainerName = "samplewebapp";

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
            //_client = new DockerClientConfiguration(new Uri("npipe://./pipe/docker_engine")).CreateClient();

            _logger.LogInformation($"Docker host: {Environment.GetEnvironmentVariable("docker_host")}");

            _client = new DockerClientConfiguration(new Uri(Environment.GetEnvironmentVariable("docker_host"))).CreateClient();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var containers = CheckForRunningContainers().Result;

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Checking for new docker image every 5 seconds. Last checked at: {time}", DateTimeOffset.Now);

                var imageParameters = new ImagesListParameters
                {
                    MatchName = _defaultContainerName
                };

                var images = await _client.Images.ListImagesAsync(imageParameters);
                foreach (var image in images.Take(1))
                {
                    var repoTag = image.RepoTags[0];
                    var dockerImage = CreateImageTag(image.ID, repoTag);

                    if (runningContainer != null)
                    {
                        if (runningContainer.ImageName != $"{ dockerImage.Label}:{dockerImage.Tag}")
                        {
                            _logger.LogInformation($"New image found with Label:Tag = {dockerImage.Label}:{dockerImage.Tag}");

                            await RemoveOldContainer(containers);

                            containers = CreateNewContainer(dockerImage.Label, dockerImage.Tag).Result;
                        }
                    }
                    else
                    {
                        containers = CreateNewContainer(dockerImage.Label, dockerImage.Tag).Result;
                    }

                }

                await Task.Delay(5000, stoppingToken);
            }
        }

        private async Task RemoveOldContainer(IList<ContainerListResponse> containers)
        {
            foreach (var container in containers)
            {
                if (container.Image == runningContainer.ImageName)
                {
                    _logger.LogInformation($"Old container with image: {container.Image} is being stopped");
                    await _client.Containers.StopContainerAsync(container.ID, new ContainerStopParameters());

                    _logger.LogInformation($"Old container with image: {container.Image} is being removed");
                    await _client.Containers.RemoveContainerAsync(container.ID, new ContainerRemoveParameters());
                }
            }
        }

        private async Task<IList<ContainerListResponse>> CheckForRunningContainers()
        {
            var containerFound = false;

            var containers = await GetAllContainers();

            foreach (var container in containers)
            {
                if (container.Names[0].Replace("/", "") == _defaultContainerName)
                {
                    runningContainer = CreateContainer(container.Names[0], container.Image, container.ID);
                    containerFound = true;
                }
            }

            if (containerFound)
            {
                _logger.LogInformation($"Already running container found with Label:Tag = {runningContainer.ImageName}");
            }

            return containers;
        }

        private async Task<IList<ContainerListResponse>> CreateNewContainer(string imageLabel, string imageTag)
        {
            var createContainerParameters = new CreateContainerParameters();
            createContainerParameters.Image = $"{imageLabel}:{imageTag}";
            createContainerParameters.Name = $"{imageLabel}";

            var pbList = new List<PortBinding>
            {
                new PortBinding {
                    HostPort="80"
                }
            };

            var portBindings = new Dictionary<string, IList<PortBinding>>
            {
                { "80/tcp", pbList }
            };

            createContainerParameters.HostConfig = new HostConfig
            {
                PortBindings = portBindings,
                PublishAllPorts = true
            };

            _logger.LogInformation($"New container with image: {createContainerParameters.Image} is being created");
            var createContainerResponse = await _client.Containers.CreateContainerAsync(createContainerParameters);

            _logger.LogInformation($"New container with image: {createContainerParameters.Image} is being started");
            await _client.Containers.StartContainerAsync(createContainerResponse.ID, null);

            runningContainer = CreateContainer($"{imageLabel}", $"{imageLabel}:{imageTag}", createContainerResponse.ID);

            var containers = await GetAllContainers();
            foreach (var container in containers)
            {
                _logger.LogInformation($"New container with image: {container.Image} is running");
            }

            return containers;
        }

        private DockerContainer CreateContainer(string name, string image, string id)
        {
            var container = new DockerContainer
            {
                Name = name,
                Id = id,
                ImageName = image
            };

            return container;
        }

        private async Task<IList<ContainerListResponse>> GetAllContainers()
        {
            var containerParameters = new ContainersListParameters
            {
                All = true
            };

            return await _client.Containers.ListContainersAsync(containerParameters);
        }

        private DockerImage CreateImageTag(string Id, string imageLabelWithTag)
        {
            var imageSplit = imageLabelWithTag.Split(":");
            var dockerImage = new DockerImage
            {
                Label = imageSplit[0],
                Tag = imageSplit[1],
                Id = Id
            };

            return dockerImage;
        }
    }

    public class DockerImage
    {
        public string Label { get; set; }
        public string Tag { get; set; }
        public string Id { get; set; }
    }

    public class DockerContainer
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ImageName { get; set; }
        public bool IsRunning { get; set; }
    }
}
