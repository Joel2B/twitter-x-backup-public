$ErrorActionPreference = "Stop"

$image = "192.168.2.141:5000/twitter-x-backup:latest"

docker build -t $image .

if ($LASTEXITCODE -ne 0) {
    throw "docker build failed with exit code $LASTEXITCODE"
}

docker push $image

if ($LASTEXITCODE -ne 0) {
    throw "docker push failed with exit code $LASTEXITCODE"
}
