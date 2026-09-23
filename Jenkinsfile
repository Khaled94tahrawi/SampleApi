pipeline {
  agent {
    kubernetes {
      yaml """
apiVersion: v1
kind: Pod
spec:
  serviceAccountName: jenkins-deployer
  containers:
  - name: dotnet
    image: mcr.microsoft.com/dotnet/sdk:8.0
    command: ['cat']
    tty: true
  - name: kaniko
    image: gcr.io/kaniko-project/executor:debug
    command: ['/busybox/cat']
    tty: true
    volumeMounts:
    - name: docker-config
      mountPath: /kaniko/.docker
  - name: kubectl
    image: bitnami/kubectl:latest
    command: ['cat']
    tty: true
  volumes:
  - name: docker-config
    secret:
      secretName: dockerhub-dockerconfigjson
      items:
        - key: .dockerconfigjson
          path: config.json
"""
    }
  }
  triggers {
    pollSCM('H/2 * * * *')
  }
  environment {
    IMAGE = "khaled94th/sampleapi-api"
    TAG   = "1.${BUILD_NUMBER}"
  }
  stages {
    stage('Checkout') {
      steps { checkout scm }
    }
    stage('Build & Test .NET') {
      steps {
        container('dotnet') {
          sh 'dotnet restore SampleApi.csproj'
          sh 'dotnet build SampleApi.csproj -c Release'
        }
      }
    }
    stage('Build & Push Image') {
      steps {
        container('kaniko') {
          sh """
            /kaniko/executor \\
              --context=`pwd` \\
              --dockerfile=`pwd`/Dockerfile \\
              --destination=${IMAGE}:${TAG} \\
              --destination=${IMAGE}:latest
          """
        }
      }
    }
    stage('Deploy to Kubernetes') {
      steps {
        container('kubectl') {
          sh "kubectl set image deployment/sample-dotnet-api api=${IMAGE}:${TAG} -n monitoring"
          sh "kubectl rollout status deployment/sample-dotnet-api -n monitoring"
        }
      }
    }
  }
}
