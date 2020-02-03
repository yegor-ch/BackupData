# BackupData
Program for data backup 

## General info

The backup program consists of a client and server part.
The client program scans the specified directory and creates a file with a hash-value of all the files in the directory. 
A similar scan is performed on the server side of the application. If the hash-values of the files of the client part differ from the values on the server, the program backs up the files that have been changed.

## Technologies

* C#
* Socket
* OSI model, TCP
* XML serialization, deserialization
