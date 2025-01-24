#!/bin/bash

check_failure() {
    if [ $? -ne 0 ]
    then
        echo "There's something wrong!!!";
        exit -1;
    fi
}

mkdir build
g++ -I../linux64 -L../linux64 marketdata/src/marketdata.cpp -lthostmduserapi -o build/marketdata.exe
check_failure
export LD_LIBRARY_PATH=`pwd`/../linux64
echo "LD_LIBRARY_PATH is ${LD_LIBRARY_PATH}"
cd build
./marketdata.exe

