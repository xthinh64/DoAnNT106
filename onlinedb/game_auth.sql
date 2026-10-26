-- SQLBook: Code
CREATE DATABASE IF NOT EXISTS onlinechessdb CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE onlinechessdb;

-- 1. Users - Tài khoản
CREATE TABLE IF NOT EXISTS Users (
    user_id INT AUTO_INCREMENT PRIMARY KEY,
    username VARCHAR(50) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    display_name VARCHAR(100),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 2. UserGameStats - Điểm và thống kê (Tách biệt Cờ Vua & Cờ Caro)
CREATE TABLE IF NOT EXISTS UserGameStats (
    stat_id INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    game_type ENUM('CHESS', 'CARO') NOT NULL,
    elo INT DEFAULT 1200,
    wins INT DEFAULT 0,
    losses INT DEFAULT 0,
    draws INT DEFAULT 0,
    FOREIGN KEY (user_id) REFERENCES Users(user_id) ON DELETE CASCADE,
    UNIQUE KEY unique_user_game (user_id, game_type)
);

-- 3. Rooms - Phòng chơi
CREATE TABLE IF NOT EXISTS Rooms (
    room_id INT AUTO_INCREMENT PRIMARY KEY,
    room_code VARCHAR(10) UNIQUE NOT NULL,
    game_type ENUM('CHESS', 'CARO') NOT NULL,
    status ENUM('WAITING', 'PLAYING', 'FINISHED') DEFAULT 'WAITING',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 4. RoomMembers - Thành viên phòng
CREATE TABLE IF NOT EXISTS RoomMembers (
    member_id INT AUTO_INCREMENT PRIMARY KEY,
    room_id INT NOT NULL,
    user_id INT NULL,
    role ENUM('HOST', 'PLAYER', 'SPECTATOR') DEFAULT 'SPECTATOR',
    joined_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (room_id) REFERENCES Rooms(room_id) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES Users(user_id) ON DELETE SET NULL
);

-- 5. Matches - Trận đấu
CREATE TABLE IF NOT EXISTS Matches (
    match_id INT AUTO_INCREMENT PRIMARY KEY,
    room_id INT NULL,
    game_type ENUM('CHESS', 'CARO') NOT NULL,
    mode ENUM('ONLINE', 'OFFLINE', 'PRACTICE') DEFAULT 'ONLINE',
    status ENUM('IN_PROGRESS', 'COMPLETED', 'ABORTED') DEFAULT 'IN_PROGRESS',
    started_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    ended_at TIMESTAMP NULL,
    FOREIGN KEY (room_id) REFERENCES Rooms(room_id) ON DELETE SET NULL
);

-- 6. MatchPlayers - Người chơi trong trận
CREATE TABLE IF NOT EXISTS MatchPlayers (
    match_player_id INT AUTO_INCREMENT PRIMARY KEY,
    match_id INT NOT NULL,
    user_id INT NOT NULL,
    player_color VARCHAR(10),
    result ENUM('WIN', 'LOSS', 'DRAW') NULL,
    elo_before INT,
    elo_after INT,
    FOREIGN KEY (match_id) REFERENCES Matches(match_id) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES Users(user_id) ON DELETE CASCADE
);

-- 7. RoomMessages - Tin nhắn phòng
CREATE TABLE IF NOT EXISTS RoomMessages (
    message_id INT AUTO_INCREMENT PRIMARY KEY,
    room_id INT NOT NULL,
    user_id INT NULL,
    sender_name VARCHAR(100) NOT NULL,
    message_text TEXT NOT NULL,
    sent_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (room_id) REFERENCES Rooms(room_id) ON DELETE CASCADE
);
CREATE TABLE IF NOT EXISTS MatchmakingQueue (
    queue_id INT AUTO_INCREMENT PRIMARY KEY,
    username VARCHAR(50) NOT NULL,
    game_type ENUM('CHESS', 'CARO') NOT NULL,
    elo INT NOT NULL,
    joined_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);