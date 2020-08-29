## Getting Started

Step 1:
Build the solution

Step 2:
Open CMD and check you have docker installed
```
docker --version
```

Step 3:
In Visual Studio, run the DockerUpgradeService application. Its a worker application that will open in console and will run continously

Step 4:
Back in CMD, change directory to SampleWebApp and run the following command
```
docker build -t samplewebapp:0.0.0 .
```

Step 5:
Check the worker console application. You should see the image "samplewebapp:0.0.0 getting picked up and running"

Step 6:
Go to http://localhost on local to check out the samplewebapp

Step 6:
Make any changes to SampleWebApp and create a new docker image by running this command. Make sure to increment the tag otherwise the changes will not be picked by
```
docker build -t samplewebapp:0.0.1 .
```

Step 7:
In few seconds the new image will be picked up by the worker service. Refresh the web browser to see your changes.

Step 8:
Repeat as many times as you like. Just remember to increment the image tag for new changes.
