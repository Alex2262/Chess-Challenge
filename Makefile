all: 
	dotnet publish -c Release Chess-Challenge -o ./chess-challenge-antares/ -p:PublishSingleFile=true --self-contained true

